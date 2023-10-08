using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using KdSoft.EtwLogging;
using Microsoft.Extensions.Logging;

namespace KdSoft.EtwEvents.EventSinks
{
    // There is no .NET Seq-Api needed for sending data, for an examples see
    // - https://github.com/datalust/nlog-targets-seq, look at SeqTarget.cs
    // - https://github.com/serilog/serilog-sinks-seq
    // See also https://docs.datalust.co/docs/posting-raw-events
    public class SeqSink: IEventSink
    {
        public const string BulkUploadResource = "api/events/raw?clef";
        public const string ApiKeyHeaderName = "X-Seq-ApiKey";

        public static Regex MinLevelRegex = new Regex(@"""MinimumLevelAccepted""\s*:\s*""(.+)""\s*[,\}]", RegexOptions.Compiled);
        public static Regex ErrorRegex = new Regex(@"""Error""\s*:\s*""(.+)""\s*[,\}]", RegexOptions.Compiled);

        readonly HttpClient _http;
        readonly IEventSinkContext _context;
        readonly JsonWriterOptions _jsonOptions;
        readonly ArrayBufferWriter<byte> _bufferWriter;
        readonly Utf8JsonWriter _jsonWriter;
        readonly TaskCompletionSource<bool> _tcs;
        readonly ReadOnlyMemory<byte> _newLine = "\n"u8.ToArray();
        readonly Uri _requestUri;

        // TraceEventLevel values are ordered opposite to SeqLogLevel values!
        TraceEventLevel? _maxTraceEventLevel;
        int _isDisposed = 0;

        public Task<bool> RunTask { get; }

        public SeqSink(HttpClient http, Uri requestUri, TraceEventLevel? maxLevel, IEventSinkContext context) {
            this._http = http;
            this._requestUri = requestUri;
            this._maxTraceEventLevel = maxLevel;
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
                    _context.Logger.LogError(ex, "Error closing event sink '{eventSink}'.", nameof(SeqSink));
                }
                _tcs.TrySetResult(true);
            }
        }

        // Warning: ValueTasks should not be awaited multiple times
        public ValueTask DisposeAsync() {
            Dispose();
            return ValueTask.CompletedTask;
        }

        internal static async Task<SeqLogLevel?> PostAsync(HttpClient http, Uri requestUri, ReadOnlyMemory<byte> eventBatch) {
            var response = await http.PostAsync(requestUri, new ReadOnlyMemoryContent(eventBatch)).ConfigureAwait(false);
            if (response.IsSuccessStatusCode) {
                // parse server's mimimum accepted level and use it for future requests
                // might send an empty message first (after construction), to get this returned.
                var levelString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var match = MinLevelRegex.Match(levelString);
                if (match.Success && Enum.TryParse(match.Groups[1].Value, out SeqLogLevel minLevel)) {
                    return minLevel;
                }
                return null;
            }
            else {
                var errorJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var errorMsg = ErrorRegex.Match(errorJson).Groups[1].Value;
                throw new HttpRequestException(errorMsg, null, response.StatusCode);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool ExcludeEvent(EtwEvent evt) {
            var maxLevel = _maxTraceEventLevel ?? TraceEventLevel.Always;
            if (maxLevel == TraceEventLevel.Always)
                return false;
            return evt.Level > maxLevel;
        }

        // writes a JSON object to the buffer, terminating with a new line
        // see https://github.com/serilog/serilog-formatting-compact
        void WriteEventJson(EtwEvent evt) {
            if (ExcludeEvent(evt))
                return;

            _jsonWriter.Reset();
            _jsonWriter.WriteStartObject();

            //TODO what's the best way in Seq to specify a logging source/site?
            _jsonWriter.WriteString("site", _context.SiteName);
            _jsonWriter.WriteString("providerName", evt.ProviderName);
            _jsonWriter.WriteNumber("channel", evt.Channel);
            _jsonWriter.WriteNumber("@i", evt.Id);
            _jsonWriter.WriteNumber("keywords", evt.Keywords);
            _jsonWriter.WriteString("@l", evt.Level.ToString());
            _jsonWriter.WriteNumber("opcode", evt.Opcode);
            _jsonWriter.WriteString("opcodeName", evt.OpcodeName);
            _jsonWriter.WriteString("taskName", evt.TaskName);
            if (evt.TimeStamp == null)
                _jsonWriter.WriteString("@t", DateTimeOffset.UtcNow.ToString("o"));
            else {
                _jsonWriter.WriteString("@t", evt.TimeStamp.ToDateTimeOffset().ToString("o"));
            }
            _jsonWriter.WriteNumber("version", evt.Version);

            _jsonWriter.WriteStartObject("payload");
            foreach (var payload in evt.Payload) {
                _jsonWriter.WriteString(payload.Key, payload.Value);
            }
            _jsonWriter.WriteEndObject();

            _jsonWriter.WriteEndObject();
            _jsonWriter.Flush();

            _bufferWriter.Write(_newLine.Span);
        }

        async Task<bool> FlushAsyncInternal(ReadOnlyMemory<byte> evtBatchBytes) {
            var minSeqLevel = await PostAsync(_http, _requestUri, evtBatchBytes).ConfigureAwait(false);
            if (minSeqLevel != null) {
                this._maxTraceEventLevel = FromSeqLogLevel(minSeqLevel.Value);
            }
            return true;
        }

        public async ValueTask<bool> WriteAsync(EtwEventBatch evtBatch) {
            if (IsDisposed || RunTask.IsCompleted)
                return false;
            try {
                _bufferWriter.Clear();
                foreach (var evt in evtBatch.Events) {
                    WriteEventJson(evt);
                }
                // flush
                var evtBatchBytes = _bufferWriter.WrittenMemory;
                if (evtBatchBytes.IsEmpty)
                    return true;
                return await FlushAsyncInternal(evtBatchBytes).ConfigureAwait(false);
            }
            catch (Exception ex) {
                _tcs.TrySetException(ex);
                return false;
            }
            finally {
                _bufferWriter.Clear();
            }
        }

        // Ordered opposite to SeqLogLevel, except for Always
        //public enum TraceEventLevel
        //{
        //    Always = 0,
        //    Critical = 1,
        //    Error = 2,
        //    Warning = 3,
        //    Informational = 4,
        //    Verbose = 5
        //}

        public enum SeqLogLevel
        {
            Verbose,
            Debug,
            Information,
            Warning,
            Error,
            Fatal
        }

        public static TraceEventLevel FromSeqLogLevel(SeqLogLevel level) {
            return level switch {
                SeqLogLevel.Verbose => TraceEventLevel.Always,
                SeqLogLevel.Debug => TraceEventLevel.Verbose,
                SeqLogLevel.Information => TraceEventLevel.Informational,
                SeqLogLevel.Warning => TraceEventLevel.Warning,
                SeqLogLevel.Error => TraceEventLevel.Error,
                SeqLogLevel.Fatal => TraceEventLevel.Critical,
                _ => TraceEventLevel.Critical,
            };
        }

        public static SeqLogLevel FromTraceEventLevel(TraceEventLevel level) {
            return level switch {
                TraceEventLevel.Always => SeqLogLevel.Verbose,
                TraceEventLevel.Verbose => SeqLogLevel.Debug,
                TraceEventLevel.Informational => SeqLogLevel.Information,
                TraceEventLevel.Warning => SeqLogLevel.Warning,
                TraceEventLevel.Error => SeqLogLevel.Error,
                TraceEventLevel.Critical => SeqLogLevel.Fatal,
                _ => SeqLogLevel.Fatal,
            };
        }
    }
}
