using System;
using System.Buffers;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EtwEvents.WebClient.Models;
using KdSoft.EtwLogging;
using Microsoft.Extensions.Options;

namespace EtwEvents.WebClient
{
    class WebSocketSink: IEventSink, IDisposable
    {
        readonly WebSocket _webSocket;
        readonly JsonWriterOptions _jsonOptions;
        readonly ArrayBufferWriter<byte> _bufferWriter;
        readonly Utf8JsonWriter _jsonWriter;
        readonly Stopwatch _stw;

        bool _startNewMessage;
        TimeSpan _pushFrequency;
        IDisposable _pushFrequencyMonitor;

        Task _receiveTask = Task.CompletedTask;
        public Task ReceiveTask => _receiveTask;

        public WebSocketSink(WebSocket webSocket, IOptionsMonitor<EventSessionOptions> optionsMonitor) {
            this._webSocket = webSocket;
            _jsonOptions = new JsonWriterOptions {
                Indented = false,
                SkipValidation = true,
            };
            _bufferWriter = new ArrayBufferWriter<byte>(512);
            _jsonWriter = new Utf8JsonWriter(_bufferWriter, _jsonOptions);
            _stw = new Stopwatch();

            this._pushFrequency = optionsMonitor.CurrentValue.PushFrequency;
            this._pushFrequencyMonitor = optionsMonitor.OnChange((opts, name) => {
                Interlocked.MemoryBarrier();
                this._pushFrequency = opts.PushFrequency;
                Interlocked.MemoryBarrier();
            });
        }

        // we only expect to receive Close messages
        async Task KeepReceiving(CancellationToken stoppingToken) {
            var receiveSegment = WebSocket.CreateServerBuffer(4096);
            WebSocketReceiveResult response;
            try {
                while (_webSocket.State != WebSocketState.Closed && _webSocket.State != WebSocketState.Aborted && !stoppingToken.IsCancellationRequested) {
                    response = await _webSocket.ReceiveAsync(receiveSegment, stoppingToken).ConfigureAwait(false);
                    if (!stoppingToken.IsCancellationRequested) {
                        switch (response.MessageType) {
                            case WebSocketMessageType.Close:
                                await CloseAsync(false).ConfigureAwait(false);
                                break;
                            case WebSocketMessageType.Text:
                                continue;
                            case WebSocketMessageType.Binary:
                                // ignore
                                continue;
                        }
                    }
                };
            }
            catch (WebSocketException wsex) when (wsex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely) {
                //
            }
        }

        public void Initialize(CancellationToken stoppingToken) {
            _stw.Restart();
            _startNewMessage = true;
            _jsonWriter.Reset();
            this._receiveTask = KeepReceiving(stoppingToken);
        }

        async Task<bool> WriteAsync(ReadOnlyMemory<byte> buffered, bool endOfMessage, CancellationToken stoppingToken) {
            try {
                await _webSocket.SendAsync(buffered, WebSocketMessageType.Text, endOfMessage, stoppingToken).ConfigureAwait(false);
                return true;
            }
            catch (WebSocketException wsex) when (wsex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely) {
                Debug.WriteLine("Premature");
                return false;
            }
        }

        public async Task<bool> WriteAsync(EtwEvent evt, long sequenceNo, CancellationToken stoppingToken) {
            if (_webSocket.State != WebSocketState.Open)
                return false;

            _bufferWriter.Clear();

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

            Interlocked.MemoryBarrier();
            if (_stw.Elapsed > _pushFrequency) {
                Interlocked.MemoryBarrier();
                _startNewMessage = true;
                _jsonWriter.WriteEndArray();
                _jsonWriter.Flush();

                var written = _bufferWriter.WrittenMemory;
                bool isOpen = await WriteAsync(written, true, stoppingToken).ConfigureAwait(false);
                if (!isOpen)
                    return false;

                _jsonWriter.Reset();
                _stw.Restart();
            }
            else {
                _jsonWriter.Flush();

                var written = _bufferWriter.WrittenMemory;
                bool isOpen = await WriteAsync(written, false, stoppingToken).ConfigureAwait(false);
                if (!isOpen)
                    return false;
            }
            return true;
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

                await this._receiveTask.ConfigureAwait(false);
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

        public void Dispose() {
            _pushFrequencyMonitor?.Dispose();
            _webSocket.Dispose();
        }

        public ValueTask DisposeAsync() {
            return CloseAsync(true /*, WebSocketCloseStatus.Empty */);
        }
    }
}
