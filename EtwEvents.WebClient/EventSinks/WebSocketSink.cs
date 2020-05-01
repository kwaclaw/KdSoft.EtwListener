using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KdSoft.EtwLogging;

namespace KdSoft.EtwEvents.WebClient.EventSinks
{
    class WebSocketSink: IEventSink
    {
        readonly WebSocket _webSocket;
        readonly JsonWriterOptions _jsonOptions;
        readonly ArrayBufferWriter<byte> _bufferWriter;
        readonly Utf8JsonWriter _jsonWriter;

        bool _startNewMessage;
        CancellationToken _cancelToken;

        Task<bool> _receiveTask = Task.FromResult(false);
        public Task<bool> RunTask => _receiveTask;

        public string Name { get; }

        public WebSocketSink(string name, WebSocket webSocket, CancellationToken cancelToken) {
            this.Name = name;
            this._webSocket = webSocket;
            _jsonOptions = new JsonWriterOptions {
                Indented = false,
                SkipValidation = true,
            };
            _bufferWriter = new ArrayBufferWriter<byte>(512);
            _jsonWriter = new Utf8JsonWriter(_bufferWriter, _jsonOptions);
            Initialize(cancelToken);
        }

        // we only expect to receive Close messages
        async Task<bool> KeepReceiving(CancellationToken stoppingToken) {
            var receiveSegment = WebSocket.CreateServerBuffer(4096);
            bool result = false;
            WebSocketReceiveResult response;
            try {
                while (_webSocket.State != WebSocketState.Closed && _webSocket.State != WebSocketState.Aborted && !stoppingToken.IsCancellationRequested) {
                    response = await _webSocket.ReceiveAsync(receiveSegment, stoppingToken).ConfigureAwait(false);
                    if (!stoppingToken.IsCancellationRequested) {
                        switch (response.MessageType) {
                            case WebSocketMessageType.Close:
                                await CloseAsync(false).ConfigureAwait(false);
                                result = true;
                                break;
                            case WebSocketMessageType.Text:
                                continue;
                            case WebSocketMessageType.Binary:
                                // ignore
                                continue;
                        }
                    }
                };
                result = _webSocket.State == WebSocketState.Closed || _webSocket.State == WebSocketState.Aborted;
            }
            catch (WebSocketException wsex) when (wsex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely) {
                result = false;
            }
            return result;
        }

        void Initialize(CancellationToken cancelToken) {
            _startNewMessage = true;
            _bufferWriter.Clear();
            _jsonWriter.Reset();
            this._cancelToken = cancelToken;
            this._receiveTask = KeepReceiving(cancelToken);
        }

        async Task<bool> WriteAsync(bool endOfMessage) {
            try {
                _jsonWriter.Flush();
                var written = _bufferWriter.WrittenMemory;
                await _webSocket.SendAsync(written, WebSocketMessageType.Text, endOfMessage, this._cancelToken).ConfigureAwait(false);
                _bufferWriter.Clear();
                return true;
            }
            catch (WebSocketException wsex) when (wsex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely) {
                Debug.WriteLine("Premature");
                return false;
            }
        }

        public ValueTask<bool> WriteAsync(EtwEvent evt, long sequenceNo) {
            if (_webSocket.State != WebSocketState.Open)
                return new ValueTask<bool>(false);

            if (_startNewMessage) {
                _startNewMessage = false;
                _jsonWriter.WriteStartArray();
            }

            _jsonWriter.WriteStartObject();

            _jsonWriter.WriteNumber("sequenceNo", sequenceNo);
            _jsonWriter.WriteString("providerName", evt.ProviderName);
            _jsonWriter.WriteNumber("channel", evt.Channel);
            _jsonWriter.WriteNumber("id", evt.Id);
            _jsonWriter.WriteNumber("keywords", evt.Keywords);
            _jsonWriter.WriteNumber("level", (uint)evt.Level);
            _jsonWriter.WriteNumber("opcode", evt.Opcode);
            _jsonWriter.WriteString("opcodeName", evt.OpcodeName);
            _jsonWriter.WriteString("taskName", evt.TaskName);
            // timeStamp will be passed as milliseconds to Javascript
            var timeStamp = (evt.TimeStamp.Seconds * 1000) + (evt.TimeStamp.Nanos / 1000000);
            _jsonWriter.WriteNumber("timeStamp", timeStamp);
            _jsonWriter.WriteNumber("version", evt.Version);

            _jsonWriter.WriteStartObject("payload");
            foreach (var payload in evt.Payload) {
                _jsonWriter.WriteString(payload.Key, payload.Value);
            }
            _jsonWriter.WriteEndObject();

            _jsonWriter.WriteEndObject();

            return new ValueTask<bool>(WriteAsync(false));
        }

        // Warning: ValueTasks should not be awaited multiple times
        async ValueTask CloseAsync(bool initiate, WebSocketCloseStatus status = WebSocketCloseStatus.NormalClosure) {
            if (_webSocket.State == WebSocketState.Closed)
                return;

            try {
                if (initiate)
                    await _webSocket.CloseAsync(status, "Close", default(CancellationToken)).ConfigureAwait(false);
                else
                    await _webSocket.CloseOutputAsync(status, "ACK Close", default(CancellationToken)).ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                // typically ignored in this scenario
            }
            finally {
                // don't leave the socket in any potentially connected state
                if (_webSocket.State != WebSocketState.Closed)
                    _webSocket.Abort();
                this.Dispose();
            }
        }

        async Task<bool> FlushAsyncInternal() {
            if (_webSocket.State != WebSocketState.Open)
                return false;

            // don't write an end-of-message if nothing has accumulated
            if (_startNewMessage)
                return true;

            _startNewMessage = true;
            _jsonWriter.WriteEndArray();

            bool isOpen = await WriteAsync(true).ConfigureAwait(false);
            if (!isOpen)
                return false;

            _jsonWriter.Reset();
            return true;
        }

        public ValueTask<bool> FlushAsync() {
            return new ValueTask<bool>(FlushAsyncInternal());
        }

        public void Dispose() {
            _jsonWriter?.Dispose();
            _webSocket.Dispose();
        }

        public async ValueTask DisposeAsync() {
            try {
                await CloseAsync(true /*, WebSocketCloseStatus.Empty */).ConfigureAwait(false);
            }
            catch (Exception ex) {
                //TODO log exception somewhere
            }
        }

        public bool Equals([AllowNull] IEventSink other) {
            if (object.ReferenceEquals(this, other))
                return true;
            if (other == null)
                return false;
            return StringComparer.Ordinal.Equals(this.Name, other.Name);
        }
    }
}
