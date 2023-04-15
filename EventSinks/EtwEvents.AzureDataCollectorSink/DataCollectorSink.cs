using System.Buffers;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KdSoft.EtwLogging;
using Microsoft.Extensions.Logging;

namespace KdSoft.EtwEvents.EventSinks
{
    // HTTP Data Collector API:  https://docs.microsoft.com/en-ca/azure/azure-monitor/logs/data-collector-api
    public class DataCollectorSink: IEventSink
    {
        readonly HttpClient _http;
        readonly IEventSinkContext _context;
        readonly JsonWriterOptions _jsonOptions;
        readonly ArrayBufferWriter<byte> _bufferWriter;
        readonly Utf8JsonWriter _jsonWriter;
        readonly TaskCompletionSource<bool> _tcs;
        readonly Uri _requestUri;
        readonly DataCollectorSinkOptions _options;
        readonly string _sharedKey;

        int _isDisposed = 0;

        public Task<bool> RunTask { get; }

        public DataCollectorSink(
            HttpClient http,
            Uri requestUri,
            DataCollectorSinkOptions options,
            string sharedKey,
            IEventSinkContext context
        ) {
            this._http = http;
            this._requestUri = requestUri;
            this._options = options;
            this._sharedKey = sharedKey;
            this._context = context;

            _tcs = new TaskCompletionSource<bool>();
            RunTask = _tcs.Task;

            _jsonOptions = new JsonWriterOptions {
                Indented = false,
                SkipValidation = true,
            };
            _bufferWriter = new ArrayBufferWriter<byte>(1024);
            _jsonWriter = new Utf8JsonWriter(_bufferWriter, _jsonOptions);
        }

        public bool IsDisposed {
            get {
                Interlocked.MemoryBarrier();
                var isDisposed = this._isDisposed;
                Interlocked.MemoryBarrier();
                return isDisposed > 0;
            }
        }

        public void Dispose() {
            var oldDisposed = Interlocked.CompareExchange(ref _isDisposed, 99, 0);
            if (oldDisposed == 0) {
                try {
                    _http.Dispose();
                    _jsonWriter?.Dispose();
                }
                catch (Exception ex) {
                    _context.Logger.LogError(ex, "Error closing event sink '{eventSink}'.", nameof(DataCollectorSink));
                }
                _tcs.TrySetResult(true);
            }
        }

        // Warning: ValueTasks should not be awaited multiple times
        public ValueTask DisposeAsync() {
            Dispose();
            return ValueTask.CompletedTask;
        }

        static readonly byte[] _keyBuffer = new byte[256];
        static readonly byte[] _msgBuffer = new byte[256];
        static readonly byte[] _hashBuffer = new byte[64];

        public static string SignatureHash(string message, string secret) {
            var encoding = new ASCIIEncoding();
            if (!Convert.TryFromBase64String(secret, _keyBuffer, out var keyByteCount)) {
                throw new FormatException();
            }

            var msgByteCount = encoding.GetBytes(message, _msgBuffer);

            var hashByteCount = HMACSHA256.HashData(
                new ReadOnlySpan<byte>(_keyBuffer, 0, keyByteCount),
                new ReadOnlySpan<byte>(_msgBuffer, 0, msgByteCount),
                _hashBuffer
            );

            return Convert.ToBase64String(_hashBuffer, 0, hashByteCount);
        }

        string BuildSignature(ReadOnlySpan<byte> jsonBytes, string dateString) {
            var stringToHash = $"POST\n{jsonBytes.Length}\napplication/json\nx-ms-date:{dateString}\n/api/logs";
            string hashedString = SignatureHash(stringToHash, _sharedKey);
            return $"SharedKey {_options.CustomerId}:{hashedString}";
        }

        // see https://github.com/Zimmergren/LogAnalytics.Client/blob/main/LogAnalytics.Client/LogAnalytics.Client/LogAnalyticsClient.cs
        async Task<(HttpStatusCode, string)> PostAsync(HttpClient http, Uri requestUri, ReadOnlyMemory<byte> evtBatchBytes) {
            var request = new HttpRequestMessage(HttpMethod.Post, requestUri);

            var dateString = DateTime.UtcNow.ToString("r");
            var signature = BuildSignature(evtBatchBytes.Span, dateString);
            request.Headers.Add("Authorization", signature);
            request.Headers.Add("x-ms-date", dateString);

            var content = new ReadOnlyMemoryContent(evtBatchBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            // this leads to an invalid signature error, probably because it adds to the content's length
            //content.Headers.ContentEncoding.Add(Encoding.UTF8.WebName);
            request.Content = content;

            var response = await http.SendAsync(request).ConfigureAwait(false);
            if (response.IsSuccessStatusCode) {
                return (response.StatusCode, string.Empty);
            }
            else {
                var errJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return (response.StatusCode, errJson);
            }
        }

        void WriteEventJson(EtwEvent evt) {
            _jsonWriter.WriteStartObject();

            _jsonWriter.WriteString("site", _context.SiteName);
            _jsonWriter.WriteString("providerName", evt.ProviderName);
            _jsonWriter.WriteNumber("channel", evt.Channel);
            _jsonWriter.WriteNumber("id", evt.Id);
            _jsonWriter.WriteNumber("keywords", evt.Keywords);
            _jsonWriter.WriteString("level", evt.Level.ToString());
            _jsonWriter.WriteNumber("opcode", evt.Opcode);
            _jsonWriter.WriteString("opcodeName", evt.OpcodeName);
            _jsonWriter.WriteString("taskName", evt.TaskName);
            if (evt.TimeStamp == null)
                _jsonWriter.WriteString("timeStamp", DateTimeOffset.UtcNow.ToString("o"));
            else {
                _jsonWriter.WriteString("timeStamp", evt.TimeStamp.ToDateTimeOffset().ToString("o"));
            }
            _jsonWriter.WriteNumber("version", evt.Version);

            _jsonWriter.WriteStartObject("payload");
            foreach (var payload in evt.Payload) {
                _jsonWriter.WriteString(payload.Key, payload.Value);
            }
            _jsonWriter.WriteEndObject();

            _jsonWriter.WriteEndObject();
        }

        void WriteEventBatchJson(EtwEventBatch evtBatch) {
            _jsonWriter.Reset();
            _jsonWriter.WriteStartArray();
            foreach (var evt in evtBatch.Events) {
                WriteEventJson(evt);
            }
            _jsonWriter.WriteEndArray();
            _jsonWriter.Flush();
        }

        public async ValueTask<bool> WriteAsync(EtwEventBatch evtBatch) {
            if (IsDisposed || RunTask.IsCompleted)
                return false;
            try {
                _bufferWriter.Clear();
                WriteEventBatchJson(evtBatch);
                var evtBatchBytes = _bufferWriter.WrittenMemory;
                if (evtBatchBytes.IsEmpty)
                    return true;

                // flush
                var (statusCode, errorJson) = await PostAsync(_http, _requestUri, evtBatchBytes).ConfigureAwait(false);
                if (string.IsNullOrEmpty(errorJson))
                    return true;

                // see "Return codes" in https://docs.microsoft.com/en-ca/azure/azure-monitor/logs/data-collector-api
                var errorObject = JsonDocument.Parse(errorJson);
                DataCollectorSinkException ex;
                if (errorObject != null) {
                    string msg = string.Empty;
                    if (errorObject.RootElement.TryGetProperty("Message", out var msgElement)) {
                        msg = msgElement.GetString() ?? "";
                    }
                    string error = string.Empty;
                    if (errorObject.RootElement.TryGetProperty("Error", out var errElement)) {
                        error = errElement.GetString() ?? "";
                    }
                    ex = new DataCollectorSinkException(statusCode, msg, error);
                }
                else {
                    ex = new DataCollectorSinkException(statusCode);
                }
                _tcs.TrySetException(ex);
                return false;
            }
            catch (Exception ex) {
                _tcs.TrySetException(ex);
                return false;
            }
            finally {
                _bufferWriter.Clear();
            }
        }
    }
}