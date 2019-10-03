using System;
using System.Buffers;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using KdSoft.EtwLogging;

namespace EtwEvents.WebClient
{
    public sealed class EventSession: IAsyncDisposable, IDisposable
    {
        readonly EtwListener.EtwListenerClient _etwClient;
        readonly WebSocket _webSocket;
        readonly EtwEventRequest _request;
        readonly Task _receiveTask;
        readonly CancellationTokenSource _stoppingCts;

        public EventSession(EtwListener.EtwListenerClient etwClient, WebSocket webSocket, EtwEventRequest request) {
            this._etwClient = etwClient;
            this._webSocket = webSocket;
            this._request = request;
            this._receiveTask = KeepReceiving();
            this._stoppingCts = new CancellationTokenSource();
        }

        async Task KeepReceiving() {
            var recBuf = new byte[2048];
            WebSocketReceiveResult response;
            while (_webSocket.State == WebSocketState.Open) {
                var receiveSegment = new ArraySegment<byte>(recBuf);
                response = await _webSocket.ReceiveAsync(receiveSegment, CancellationToken.None).ConfigureAwait(false);
                switch (response.MessageType) {
                    case WebSocketMessageType.Close:
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None).ConfigureAwait(false);
                        break;
                    case WebSocketMessageType.Text:
                        break;
                    case WebSocketMessageType.Binary:
                        // ignore
                        break;
                }
            };
        }

        async Task Run(CancellationToken stoppingToken) {
            var jsonOptions = new JsonWriterOptions {
                Indented = false,
                SkipValidation = true,
            };
            var bufferWriter = new ArrayBufferWriter<byte>(512);
            using (var jsonWriter = new Utf8JsonWriter(bufferWriter, jsonOptions)) {

                using (var streamer = _etwClient.GetEvents(_request)) {
                    while (await streamer.ResponseStream.MoveNext().ConfigureAwait(false)) {
                        if (_webSocket.CloseStatus.HasValue)
                            break;

                        bufferWriter.Clear();
                        jsonWriter.Reset();
                        jsonWriter.WriteStartObject();

                        var evt = streamer.ResponseStream.Current;
                        jsonWriter.WriteString("providerName", evt.ProviderName);
                        jsonWriter.WriteNumber("channel", evt.Channel);
                        jsonWriter.WriteNumber("id", evt.Id);
                        jsonWriter.WriteNumber("keywords", evt.Keywords);
                        jsonWriter.WriteNumber("level", (uint)evt.Level);
                        jsonWriter.WriteNumber("opcode", evt.Opcode);
                        jsonWriter.WriteString("taskName", evt.TaskName);
                        jsonWriter.WriteNumber("timeStampSecs", evt.TimeStamp.Seconds);
                        jsonWriter.WriteNumber("timeStampNanos", evt.TimeStamp.Nanos);
                        jsonWriter.WriteNumber("version", evt.Version);

                        jsonWriter.WriteStartObject("payload");
                        foreach (var payload in evt.Payload) {
                            jsonWriter.WriteString(payload.Key, payload.Value);
                        }
                        jsonWriter.WriteEndObject();

                        jsonWriter.WriteEndObject();
                        jsonWriter.Flush();

                        var written = bufferWriter.WrittenMemory;
                        await _webSocket.SendAsync(written, WebSocketMessageType.Text, true, stoppingToken).ConfigureAwait(false);
                    }
                }
            }
        }

        Task? _runTask;
        public Task? RunTask => _runTask;

        ValueTask _disposeTask;
        public ValueTask DisposeTask => _disposeTask;

        int started = 0;
        public bool Start() {
            var oldStarted = Interlocked.CompareExchange(ref started, 1, 0 );
            if (oldStarted == 1)
                return false;

            if (_stoppingCts.IsCancellationRequested)
                return false;

            try {
                _stoppingCts.Token.Register(() => {
                    this._disposeTask = DisposeAsync();
                });

                this._runTask = Run(_stoppingCts.Token);
                return true;
            }
            catch {
                Interlocked.Exchange(ref started, 0);
                throw;
            }
        }

        int stopped = 0;
        // await RunTask after calling stop
        public bool Stop(TimeSpan? delay = null) {
            var oldStopped = Interlocked.CompareExchange(ref stopped, 1, 0);
            if (oldStopped == 1)
                return false;

            if (delay == null)
                _stoppingCts.Cancel();
            else
                _stoppingCts.CancelAfter(delay.Value);
            return true;
        }

        public async ValueTask DisposeAsync() {
            try {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, default(CancellationToken)).ConfigureAwait(false);
            }
            finally {
                this.Dispose();
            }
        }

        public void Dispose() {
            _webSocket.Dispose();
            _stoppingCts.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
