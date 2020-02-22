using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using EtwEvents.WebClient.Models;
using Grpc.Core;
using KdSoft.EtwLogging;
using Microsoft.Extensions.Options;

namespace EtwEvents.WebClient
{
    sealed class EventSession: IAsyncDisposable, IDisposable
    {
        readonly EtwListener.EtwListenerClient _etwClient;
        readonly IEventSink _eventSink;
        readonly EtwEventRequest _etwRequest;
        readonly Timer _flushTimer;

        AsyncServerStreamingCall<EtwEvent>? _streamingCall;
        int _pushFrequencyMillisecs;
        IDisposable _pushFrequencyMonitor;
        ActionBlock<(EtwEvent?, long)> _jobQueue;
        int _writeFailed = 0;

        int _started = 0;
        int _stopped = 0;

        public EventSession(
            EtwListener.EtwListenerClient etwClient,
            IEventSink webSocketSink,  // responsible for disposing it
            EtwEventRequest etwRequest,
            IOptionsMonitor<EventSessionOptions> optionsMonitor
        ) {
            this._etwClient = etwClient;
            this._eventSink = webSocketSink;
            this._etwRequest = etwRequest;

            this._jobQueue = new ActionBlock<(EtwEvent?, long)>(ProcessResponse, new ExecutionDataflowBlockOptions {
                EnsureOrdered = true,
                MaxDegreeOfParallelism = 1
            });

            this._flushTimer = new Timer(state => {
                _jobQueue.Post((null, 0));
            });

            this._pushFrequencyMillisecs = (int)optionsMonitor.CurrentValue.PushFrequency.TotalMilliseconds;
            this._pushFrequencyMonitor = optionsMonitor.OnChange((opts, name) => {
                Interlocked.Exchange(ref this._pushFrequencyMillisecs, (int)opts.PushFrequency.TotalMilliseconds);
            });
        }

        async Task ProcessResponse((EtwEvent? evt, long sequenceNo) args) {
            Interlocked.MemoryBarrier();
            if (this._writeFailed != 0)
                return;

            bool success;
            if (args.evt == null) {
                success = await this._eventSink.FlushAsync().ConfigureAwait(false);
                if (success) {
                    var pushFrequency = this._pushFrequencyMillisecs;
                    this._flushTimer?.Change(pushFrequency, Timeout.Infinite);
                }
            }
            else {
                success = await this._eventSink.WriteAsync(args.evt, args.sequenceNo).ConfigureAwait(false);
            }

            if (!success) {
                Interlocked.MemoryBarrier();
                this._writeFailed = 99;
                Interlocked.MemoryBarrier();
            }
        }

        async Task ProcessResponseStream(CancellationToken cancelToken) {
            this._eventSink.Initialize(cancelToken);

            long sequenceNo = 0;
            var streamer = _streamingCall = _etwClient.GetEvents(_etwRequest);
            var responseStream = streamer.ResponseStream;

            var pushFrequency = this._pushFrequencyMillisecs;
            this._flushTimer?.Change(pushFrequency, Timeout.Infinite);
            try {
                while (await responseStream.MoveNext(default(CancellationToken)).ConfigureAwait(false)) {

                    // we should not call CloseAsync while still sending
                    Interlocked.MemoryBarrier();
                    if (_stopped != 0 || _streamingCall == null || _writeFailed != 0)
                        break;

                    var evt = responseStream.Current;

                    // ignore empty messages
                    if (evt.TimeStamp == null)
                        continue;

                    if (!_jobQueue.Post((evt, sequenceNo)))
                        break;

                    sequenceNo += 1;
                }
            }
            catch (RpcException rex) when (rex.StatusCode == StatusCode.Cancelled) {
                // Expected, happens when we Dispose the AsyncServerStreamingCall<EtwEvent>,
                // which is the only way to stop the stream from the client.
            }
            finally {
                _jobQueue.Complete();
            }
        }

        public async Task<bool> Run(CancellationToken cancelToken) {
            var oldStarted = Interlocked.CompareExchange(ref _started, 1, 0);
            if (oldStarted == 1)
                return false;

            try {
                await ProcessResponseStream(cancelToken).ConfigureAwait(false);
                return true;
            }
            catch {
                Interlocked.Exchange(ref _started, 0);
                throw;
            }
            finally {
                Interlocked.Exchange(ref _stopped, 1);
                await DisposeAsync().ConfigureAwait(false);
            }
        }

        public async Task<bool> Stop() {
            var oldStopped = Interlocked.CompareExchange(ref _stopped, 1, 0);
            if (oldStopped == 1)
                return false;

            await DisposeAsync().ConfigureAwait(false);
            return true;
        }

        // Warning: ValueTasks should not be awaited multiple times
        public ValueTask DisposeAsync() {
            _pushFrequencyMonitor?.Dispose();
            _flushTimer?.Dispose();
            var oldStreamer = Interlocked.Exchange(ref _streamingCall, null);
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
            _pushFrequencyMonitor?.Dispose();
            _flushTimer?.Dispose();
            var oldStreamer = Interlocked.Exchange(ref _streamingCall, null);
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
