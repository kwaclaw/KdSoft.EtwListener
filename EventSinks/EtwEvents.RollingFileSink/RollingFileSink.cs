using System;
using System.Buffers;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using KdSoft.EtwLogging;
using KdSoft.Utils;
using Microsoft.Extensions.Logging;

namespace KdSoft.EtwEvents.EventSinks
{
    public class RollingFileSink: IEventSink
    {
        static readonly EtwEvent _emptyEvent = new EtwEvent();

        readonly JsonWriterOptions _jsonOptions;
        readonly ArrayBufferWriter<byte> _bufferWriter;
        readonly Utf8JsonWriter _jsonWriter;
        readonly ReadOnlyMemory<byte> _newLine = new byte[] { 10 };
        readonly RollingFileFactory _fileFactory;
        readonly ILogger _logger;
        readonly Channel<EtwEvent> _channel;

        int _isDisposed = 0;

        public Task<bool> RunTask { get; }

        public RollingFileSink(RollingFileFactory fileFactory, bool relaxedJsonEscaping, ILogger logger) {
            this._fileFactory = fileFactory;
            this._logger = logger;
            this._channel = Channel.CreateUnbounded<EtwEvent>(new UnboundedChannelOptions {
                AllowSynchronousContinuations = true,
                SingleReader = true,
                SingleWriter = false
            });

            _jsonOptions = new JsonWriterOptions {
                Indented = false,
                SkipValidation = true,
            };
            if (relaxedJsonEscaping)
                _jsonOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

            _bufferWriter = new ArrayBufferWriter<byte>(1024);
            _jsonWriter = new Utf8JsonWriter(_bufferWriter, _jsonOptions);

            RunTask = Process();
        }

        public bool IsDisposed {
            get {
                Interlocked.MemoryBarrier();
                var isDisposed = this._isDisposed;
                Interlocked.MemoryBarrier();
                return isDisposed > 0;
            }
        }

        // returns true if actually disposed, false if already disposed
        bool InternalDispose() {
            var oldDisposed = Interlocked.CompareExchange(ref _isDisposed, 99, 0);
            if (oldDisposed == 0) {
                // we assume this does not throw
                _channel.Writer.TryComplete();
                return true;
            }
            return false;
        }

        public void Dispose() {
            if (InternalDispose()) {
                try {
                    _channel.Reader.Completion.Wait();
                    _jsonWriter.Dispose();
                    _fileFactory.Dispose();
                }
                catch (Exception ex) {
                    _logger.LogError(ex, "Error closing event sink '{eventSink}'.", nameof(RollingFileSink));
                }
            }
        }

        // Warning: ValueTasks should not be awaited multiple times
        public async ValueTask DisposeAsync() {
            if (InternalDispose()) {
                try {
                    await _channel.Reader.Completion.ConfigureAwait(false);
                    await _jsonWriter.DisposeAsync().ConfigureAwait(false);
                    await _fileFactory.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception ex) {
                    _logger.LogError(ex, "Error closing event sink '{eventSink}'.", nameof(RollingFileSink));
                }
            }
        }

        // writes a JSON object to the buffer, terminating with a new line
        // see https://github.com/serilog/serilog-formatting-compact
        void WriteEventJson(EtwEvent evt) {
            _jsonWriter.Reset();
            _jsonWriter.WriteStartObject();

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
            _jsonWriter.Flush();

            _bufferWriter.Write(_newLine.Span);
        }

        public ValueTask<bool> FlushAsync() {
            if (IsDisposed || RunTask.IsCompleted)
                return ValueTask.FromResult(false);
            try {
                var posted = _channel.Writer.TryWrite(_emptyEvent);
                if (!posted)
                    _channel.Writer.TryComplete();
                return ValueTask.FromResult(posted);
            }
            catch (Exception ex) {
                _channel.Writer.TryComplete(ex);
                return ValueTask.FromResult(false);
            }
        }

        public ValueTask<bool> WriteAsync(EtwEvent evt) {
            if (IsDisposed || RunTask.IsCompleted)
                return ValueTask.FromResult(false);
            try {
                var posted = _channel.Writer.TryWrite(evt);
                if (!posted)
                    _channel.Writer.TryComplete();
                return ValueTask.FromResult(posted);
            }
            catch (Exception ex) {
                _channel.Writer.TryComplete(ex);
                return ValueTask.FromResult(false);
            }
        }

        public ValueTask<bool> WriteAsync(EtwEventBatch evtBatch) {
            if (IsDisposed || RunTask.IsCompleted)
                return ValueTask.FromResult(false);
            try {
                bool posted = true;
                foreach (var evt in evtBatch.Events) {
                    posted = _channel.Writer.TryWrite(evt);
                    if (!posted) {
                        _channel.Writer.TryComplete();
                        break;
                    }
                }
                return ValueTask.FromResult(posted);
            }
            catch (Exception ex) {
                _channel.Writer.TryComplete(ex);
                return ValueTask.FromResult(false);
            }
        }

        // returns true if reading is complete
        async Task<bool> ProcessBatchToBuffer() {
            await foreach (var evt in _channel.Reader.ReadAllAsync().ConfigureAwait(false)) {
                if (object.ReferenceEquals(evt, _emptyEvent))
                    return false;
                WriteEventJson(evt);
            }
            // batch is complete, we can write it now to the file
            return true;
        }

        async Task<bool> WriteBatchAsync(FileStream stream) {
            var eventBatch = _bufferWriter.WrittenMemory;
            if (eventBatch.IsEmpty)
                return true;

            await stream.WriteAsync(eventBatch).ConfigureAwait(false);
            _bufferWriter.Clear();

            await stream.FlushAsync().ConfigureAwait(false);
            return true;
        }

        async Task ProcessBatches() {
            bool isCompleted;
            do {
                FileStream? stream = null;
                try {
                    // checks rollover conditions and returns appropriate file stream
                    stream = await _fileFactory.GetCurrentFileStream().ConfigureAwait(false);
                    isCompleted = await ProcessBatchToBuffer().ConfigureAwait(false);
                }
                catch (Exception ex) {
                    isCompleted = true;
                    _channel.Writer.TryComplete(ex);
                }
                try {
                    if (stream != null)
                        await WriteBatchAsync(stream).ConfigureAwait(false);
                }
                catch (Exception ex) {
                    isCompleted = true;
                    _channel.Writer.TryComplete(ex);
                }
            } while (!isCompleted);
        }

        public async Task<bool> Process() {
            var processTask = ProcessBatches();
            await _channel.Reader.Completion.ConfigureAwait(false);
            await processTask.ConfigureAwait(false);
            return IsDisposed;
        }
    }
}
