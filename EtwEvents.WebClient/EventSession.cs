using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Grpc.Core;
using KdSoft.EtwLogging;
using Microsoft.Extensions.Options;

namespace KdSoft.EtwEvents.WebClient
{
    sealed class EventSession: IAsyncDisposable, IDisposable
    {
        readonly EtwListener.EtwListenerClient _etwClient;
        readonly EtwEventRequest _etwRequest;
        readonly Timer _flushTimer;
        readonly EventSinkHolder _eventSinks;
        readonly AggregatingNotifier<Models.TraceSessionStates> _changeNotifier;
        readonly IDisposable _pushFrequencyMonitor;
        readonly ActionBlock<(EtwEvent?, long)> _jobQueue;

        AsyncServerStreamingCall<EtwEvent>? _streamingCall;
        int _pushFrequencyMillisecs;

        int _started = 0;
        int _stopped = 0;
        public EventSession(
            EtwListener.EtwListenerClient etwClient,
            EtwEventRequest etwRequest,
            IOptionsMonitor<Models.EventSessionOptions> optionsMonitor,
            EventSinkHolder eventSinks,
            AggregatingNotifier<Models.TraceSessionStates> changeNotifier
        ) {
            this._etwClient = etwClient;
            this._etwRequest = etwRequest;
            this._eventSinks = eventSinks;
            this._changeNotifier = changeNotifier;

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
            var success = await this._eventSinks.ProcessEvent(args.evt, args.sequenceNo).ConfigureAwait(false);
            if (!success) {
                await _changeNotifier.PostNotification().ConfigureAwait(false);
            }

            if (args.evt == null) {
                var pushFrequency = this._pushFrequencyMillisecs;
                this._flushTimer?.Change(pushFrequency, Timeout.Infinite);
            }
        }

        async Task ProcessResponseStream(CancellationToken cancelToken) {
            long sequenceNo = 0;
            var streamer = _streamingCall = _etwClient.GetEvents(_etwRequest);
            var responseStream = streamer.ResponseStream;

            var pushFrequency = this._pushFrequencyMillisecs;
            this._flushTimer?.Change(pushFrequency, Timeout.Infinite);
            try {
                while (await responseStream.MoveNext(cancelToken).ConfigureAwait(false)) {

                    // we should not call CloseAsync while still sending
                    Interlocked.MemoryBarrier();
                    if (_stopped != 0 || _streamingCall == null)
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
            Dispose();
            return default;
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
        }
    }
}
