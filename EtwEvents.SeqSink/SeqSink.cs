using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using KdSoft.EtwEvents.Client.Shared;
using KdSoft.EtwLogging;

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
        readonly JsonWriterOptions _jsonOptions;
        readonly ArrayBufferWriter<byte> _bufferWriter;
        readonly Utf8JsonWriter _jsonWriter;
        readonly TaskCompletionSource<bool> _tcs;
        readonly ReadOnlyMemory<byte> _newLine = new byte[] { 10 };
        readonly Uri _requestUri;

        // TraceEventLevel values are ordered opposite to SeqLogLevel values!
        TraceEventLevel? _maxTraceEventLevel;
        int _isDisposed = 0;

        public string Name { get; }

        public Task<bool> RunTask { get; }

        public SeqSink(string name, HttpClient http, Uri requestUri, TraceEventLevel? maxLevel) {
            this.Name = name;
            this._http = http;
            this._requestUri = requestUri;
            this._maxTraceEventLevel = maxLevel;

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
                _http.Dispose();
                _jsonWriter?.Dispose();
                _tcs.TrySetResult(true);
            }
        }

        // Warning: ValueTasks should not be awaited multiple times
        public ValueTask DisposeAsync() {
            Dispose();
            return default;
        }

        public bool Equals([AllowNull] IEventSink other) {
            if (object.ReferenceEquals(this, other))
                return true;
            if (other == null)
                return false;
            return StringComparer.Ordinal.Equals(this.Name, other.Name);
        }

        public static async Task<SeqLogLevel?> PostAsync(HttpClient http, Uri requestUri, ReadOnlyMemory<byte> eventBatch) {
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void WriteEventJson(EtwEvent evt, long sequenceNo) {
            if (ExcludeEvent(evt))
                return;

            _jsonWriter.Reset();
            _jsonWriter.WriteStartObject();

            _jsonWriter.WriteNumber("sequenceNo", sequenceNo);
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

        async Task<bool> FlushAsyncInternal(ReadOnlyMemory<byte> eventBatch) {
            var minSeqLevel = await PostAsync(_http, _requestUri, eventBatch).ConfigureAwait(false);
            _bufferWriter.Clear();
            if (minSeqLevel != null) {
                this._maxTraceEventLevel = FromSeqLogLevel(minSeqLevel.Value);
            }
            return true;
        }

        public ValueTask<bool> FlushAsync() {
            if (IsDisposed)
                return new ValueTask<bool>(false);
            var eventBatch = _bufferWriter.WrittenMemory;
            if (eventBatch.IsEmpty)
                return new ValueTask<bool>(true);
            return new ValueTask<bool>(FlushAsyncInternal(eventBatch));
        }

        public ValueTask<bool> WriteAsync(EtwEvent evt, long sequenceNo) {
            if (IsDisposed)
                return new ValueTask<bool>(false);
            WriteEventJson(evt, sequenceNo);
            return new ValueTask<bool>(true);
        }

        public ValueTask<bool> WriteAsync(EtwEventBatch evtBatch, long sequenceNo) {
            if (IsDisposed)
                return new ValueTask<bool>(false);
            foreach (var evt in evtBatch.Events) {
                WriteEventJson(evt, sequenceNo++);
            }
            return new ValueTask<bool>(true);
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
            switch (level) {
                case SeqLogLevel.Verbose:
                    return TraceEventLevel.Always;
                case SeqLogLevel.Debug:
                    return TraceEventLevel.Verbose;
                case SeqLogLevel.Information:
                    return TraceEventLevel.Informational;
                case SeqLogLevel.Warning:
                    return TraceEventLevel.Warning;
                case SeqLogLevel.Error:
                    return TraceEventLevel.Error;
                case SeqLogLevel.Fatal:
                    return TraceEventLevel.Critical;
                default:
                    return TraceEventLevel.Critical;
            }
        }
        public static SeqLogLevel FromTraceEventLevel(TraceEventLevel level) {
            switch (level) {
                case TraceEventLevel.Always:
                    return SeqLogLevel.Verbose;
                case TraceEventLevel.Verbose:
                    return SeqLogLevel.Debug;
                case TraceEventLevel.Informational:
                    return SeqLogLevel.Information;
                case TraceEventLevel.Warning:
                    return SeqLogLevel.Warning;
                case TraceEventLevel.Error:
                    return SeqLogLevel.Error;
                case TraceEventLevel.Critical:
                    return SeqLogLevel.Fatal;
                default:
                    return SeqLogLevel.Fatal;
            }
        }
    }
}
