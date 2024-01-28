using System.Buffers;
using System.Net;
using System.Text.Json;
using Azure;
using Azure.Core;
using Azure.Monitor.Ingestion;
using KdSoft.EtwLogging;
using Microsoft.Extensions.Logging;

namespace KdSoft.EtwEvents.EventSinks
{
    // Azure LogsIngestion API:
    // See https://learn.microsoft.com/en-us/dotnet/api/overview/azure/monitor.ingestion-readme?view=azure-dotnet
    // and https://learn.microsoft.com/en-us/azure/azure-monitor/logs/logs-ingestion-api-overview
    public class LogsIngestionSink: IEventSink
    {
        readonly LogsIngestionClient _client;
        readonly IEventSinkContext _context;
        readonly JsonWriterOptions _jsonOptions;
        readonly ArrayBufferWriter<byte> _bufferWriter;
        readonly Utf8JsonWriter _jsonWriter;
        readonly TaskCompletionSource<bool> _tcs;
        readonly LogsIngestionSinkOptions _options;

        int _isDisposed = 0;

        public Task<bool> RunTask { get; }

        public LogsIngestionSink(
            LogsIngestionSinkOptions options,
            TokenCredential credential,
            IEventSinkContext context
        ) {
            this._options = options;
            this._context = context;

            var endpoint = new Uri(options.EndPoint);
            _client = new LogsIngestionClient(endpoint, credential);

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
                    _jsonWriter?.Dispose();
                }
                catch (Exception ex) {
                    _context.Logger.LogError(ex, "Error closing event sink '{eventSink}'.", nameof(LogsIngestionSink));
                }
                _tcs.TrySetResult(true);
            }
        }

        // Warning: ValueTasks should not be awaited multiple times
        public ValueTask DisposeAsync() {
            GC.SuppressFinalize(this);
            Dispose();
            return ValueTask.CompletedTask;
        }

        void WriteEventJson(EtwEvent evt) {
            _jsonWriter.WriteStartObject();

            _jsonWriter.WriteString("Site", _context.SiteName);
            _jsonWriter.WriteString("ProviderName", evt.ProviderName);
            _jsonWriter.WriteNumber("Channel", evt.Channel);
            _jsonWriter.WriteNumber("Id", evt.Id);
            _jsonWriter.WriteNumber("Keywords", evt.Keywords);
            _jsonWriter.WriteNumber("Level", (int)evt.Level);
            _jsonWriter.WriteNumber("Opcode", evt.Opcode);
            _jsonWriter.WriteString("OpcodeName", evt.OpcodeName);
            _jsonWriter.WriteString("TaskName", evt.TaskName);
            if (evt.TimeStamp == null)
                _jsonWriter.WriteString("TimeStamp", DateTimeOffset.UtcNow.ToString("o"));
            else {
                _jsonWriter.WriteString("TimeStamp", evt.TimeStamp.ToDateTimeOffset().ToString("o"));
            }
            _jsonWriter.WriteNumber("Version", evt.Version);
            _jsonWriter.WriteNumber("ProcessId", evt.ProcessId);
            _jsonWriter.WriteNumber("ThreadId", evt.ThreadId);
            _jsonWriter.WriteString("ProcessName", evt.ProcessName);

            _jsonWriter.WriteStartObject("Payload");
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

                var data = BinaryData.FromBytes(evtBatchBytes);
                Response response = await _client.UploadAsync(_options.RuleId, _options.StreamName, RequestContent.Create(data));

                if (!response.IsError) {
                    return true;
                }

                var ex = new LogsIngestionSinkException((HttpStatusCode)response.Status, response.ReasonPhrase, response.Content.ToString());
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