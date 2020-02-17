using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using KdSoft.EtwLogging;

namespace EtwEvents.WebClient
{
    sealed class EventSession: IAsyncDisposable, IDisposable
    {
        readonly EtwListener.EtwListenerClient _etwClient;
        readonly IEventSink _eventSink;
        readonly EtwEventRequest _request;

        AsyncServerStreamingCall<EtwEvent>? _streamer;

        public EventSession(
            EtwListener.EtwListenerClient etwClient,
            IEventSink webSocketSink,  // responsible for disposing it
            EtwEventRequest request
        ) {
            this._etwClient = etwClient;
            this._eventSink = webSocketSink;
            this._request = request;
        }

        async Task Run(IEventSink sink, CancellationToken stoppingToken) {
            sink.Initialize(stoppingToken);

            long sequenceNo = 0;
            var streamer = _streamer = _etwClient.GetEvents(_request);
            var responseStream = streamer.ResponseStream;

            try {
                while (await responseStream.MoveNext(default(CancellationToken)).ConfigureAwait(false)) {
                    var evt = responseStream.Current;

                    // we should not call CloseAsync while still sending
                    if (stopped == 1 || _streamer == null)
                        break;

                    // ignore empty messages
                    if (evt.TimeStamp == null)
                        continue;

                    var success = await sink.WriteAsync(evt, sequenceNo, stoppingToken).ConfigureAwait(false);
                    if (!success)
                        break;

                    sequenceNo += 1;
                }
            }
            catch (RpcException rex) when (rex.StatusCode == StatusCode.Cancelled) {
                // Expected, happens when we Dispose the AsyncServerStreamingCall<EtwEvent>,
                // which is the only way to stop the stream from the client.
            }
            finally {
                var st = _streamer;
                if (st != null)
                    st.Dispose();
            }
        }

        int started = 0;
        public async Task<bool> Run(CancellationToken cancelToken) {
            var oldStarted = Interlocked.CompareExchange(ref started, 1, 0);
            if (oldStarted == 1)
                return false;
            ;

            try {
                await Run(this._eventSink, cancelToken).ConfigureAwait(false);
                await _eventSink.DisposeAsync().ConfigureAwait(false);
                return true;
            }
            catch {
                Interlocked.Exchange(ref started, 0);
                throw;
            }
        }

        int stopped = 0;
        public async Task<bool> Stop() {
            var oldStopped = Interlocked.CompareExchange(ref stopped, 1, 0);
            if (oldStopped == 1)
                return false;

            await DisposeAsync().ConfigureAwait(false);
            return true;
        }

        // Warning: ValueTasks should not be awaited multiple times
        public ValueTask DisposeAsync() {
            var oldStreamer = Interlocked.Exchange(ref _streamer, null);
            if (oldStreamer != null)
                try {
                    // Dispose Grpc response stream, this is the only way to end the call from the client side
                    oldStreamer.Dispose();
                }
                catch (OperationCanceledException) {
                    // typically ignored in this scenario
                }

            return _eventSink.DisposeAsync();
        }

        public void Dispose() {
            var oldStreamer = Interlocked.Exchange(ref _streamer, null);
            if (oldStreamer != null)
                try {
                    oldStreamer.Dispose();
                }
                catch (OperationCanceledException) {
                    // typically ignored in this scenario
                }

            _eventSink.Dispose();
        }
    }
}
