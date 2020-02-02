using System;
using System.Buffers;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EtwEvents.WebClient.Models;
using Grpc.Core;
using KdSoft.EtwLogging;
using Microsoft.Extensions.Options;

namespace EtwEvents.WebClient
{
    sealed class EventSession: IAsyncDisposable, IDisposable
    {
        readonly EtwListener.EtwListenerClient _etwClient;
        readonly WebSocket _webSocket;
        readonly EtwEventRequest _request;

        TimeSpan _pushFrequency;
        IDisposable _pushFrequencyMonitor;
        AsyncServerStreamingCall<EtwEvent>? _streamer;

        public EventSession(
            EtwListener.EtwListenerClient etwClient,
            WebSocket webSocket,
            EtwEventRequest request,
            IOptionsMonitor<EventSessionOptions> optionsMonitor
        ) {
            this._etwClient = etwClient;
            this._webSocket = webSocket;
            this._request = request;
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
                                await Stop(false).ConfigureAwait(false);
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

        async Task Run(CancellationToken stoppingToken) {
            var jsonOptions = new JsonWriterOptions {
                Indented = false,
                SkipValidation = true,
            };
            var bufferWriter = new ArrayBufferWriter<byte>(512);
            long sequenceNo = 0;

            var stw = new Stopwatch();

            try {

                using (var jsonWriter = new Utf8JsonWriter(bufferWriter, jsonOptions)) {

                    var streamer = _streamer = _etwClient.GetEvents(_request);
                    var responseStream = streamer.ResponseStream;

                    try {
                        stw.Restart();
                        bool startNewMessage = true;
                        jsonWriter.Reset();

                        while (await responseStream.MoveNext(default(CancellationToken)).ConfigureAwait(false)) {
                            var evt = responseStream.Current;

                            // we should not call CloseAsync while still sending
                            if (stopped == 1 || _streamer == null)
                                break;

                            if (_webSocket.State != WebSocketState.Open)
                                break;

                            // ignore empty messages
                            if (evt.TimeStamp == null)
                                continue;

                            bufferWriter.Clear();

                            if (startNewMessage) {
                                startNewMessage = false;
                                jsonWriter.WriteStartArray();
                            }

                            jsonWriter.WriteStartObject();

                            jsonWriter.WriteNumber("sequenceNo", sequenceNo++);
                            jsonWriter.WriteString("providerName", evt.ProviderName);
                            jsonWriter.WriteNumber("channel", evt.Channel);
                            jsonWriter.WriteNumber("id", evt.Id);
                            jsonWriter.WriteNumber("keywords", evt.Keywords);
                            jsonWriter.WriteNumber("level", (uint)evt.Level);
                            jsonWriter.WriteNumber("opcode", evt.Opcode);
                            jsonWriter.WriteString("opcodeName", evt.OpcodeName);
                            jsonWriter.WriteString("taskName", evt.TaskName);
                            // timeStamp will be passed as milliseconds to Javascript
                            var timeStamp = (evt.TimeStamp.Seconds * 1000) + (evt.TimeStamp.Nanos / 1000000);
                            jsonWriter.WriteNumber("timeStamp", timeStamp);
                            jsonWriter.WriteNumber("version", evt.Version);

                            jsonWriter.WriteStartObject("payload");
                            foreach (var payload in evt.Payload) {
                                jsonWriter.WriteString(payload.Key, payload.Value);
                            }
                            jsonWriter.WriteEndObject();

                            jsonWriter.WriteEndObject();

                            Interlocked.MemoryBarrier();
                            if (stw.Elapsed > _pushFrequency) {
                                Interlocked.MemoryBarrier();
                                startNewMessage = true;
                                jsonWriter.WriteEndArray();
                                jsonWriter.Flush();

                                var written = bufferWriter.WrittenMemory;
                                await _webSocket.SendAsync(written, WebSocketMessageType.Text, true, stoppingToken).ConfigureAwait(false);

                                jsonWriter.Reset();
                                stw.Restart();
                            }
                            else {
                                jsonWriter.Flush();

                                var written = bufferWriter.WrittenMemory;
                                await _webSocket.SendAsync(written, WebSocketMessageType.Text, false, stoppingToken).ConfigureAwait(false);
                            }
                        }
                    }
                    catch (RpcException rex) when (rex.StatusCode == StatusCode.Cancelled) {
                        // expected, happens when we Dispose the AsyncServerStreamingCall<EtwEvent>
                    }
                    finally {
                        var st = _streamer;
                        if (st != null)
                            st.Dispose();
                    }
                }
            }
            catch (WebSocketException wsex) when (wsex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely) {
                Debug.WriteLine("Premature");
            }
        }

        Task _receiveTask = Task.CompletedTask;
        public Task ReceiveTask => _receiveTask;

        Task? _runTask;
        public Task? RunTask => _runTask;

        int started = 0;
        public bool Start(CancellationToken cancelToken) {
            var oldStarted = Interlocked.CompareExchange(ref started, 1, 0);
            if (oldStarted == 1)
                return false;

            try {
                this._receiveTask = KeepReceiving(cancelToken);
                this._runTask = Run(cancelToken);
                return true;
            }
            catch {
                Interlocked.Exchange(ref started, 0);
                throw;
            }
        }

        int stopped = 0;
        public async Task<bool> Stop(bool initiate) {
            var oldStopped = Interlocked.CompareExchange(ref stopped, 1, 0);
            if (oldStopped == 1)
                return false;

            await Close(initiate, WebSocketCloseStatus.NormalClosure).ConfigureAwait(false);
            return true;
        }

        // Warning: ValueTasks should not be awaited multiple times
        async ValueTask Close(bool initiate, WebSocketCloseStatus status = WebSocketCloseStatus.Empty) {
            try {
                // Dispose Grpc response stream, this is the only way to end the call from the client side
                var oldStreamer = Interlocked.Exchange(ref _streamer, null);
                if (oldStreamer != null)
                    oldStreamer.Dispose();

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

        // Warning: ValueTasks should not be awaited multiple times
        public ValueTask DisposeAsync() {
            return this.Close(_webSocket.State != WebSocketState.Closed, 0);
        }

        public void Dispose() {
            try {
                _pushFrequencyMonitor?.Dispose();
                _webSocket.Dispose();
                GC.SuppressFinalize(this);
            }
            catch {
                //
            }
        }
    }
}
