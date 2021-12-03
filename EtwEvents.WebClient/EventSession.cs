using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using KdSoft.EtwEvents.Client;
using KdSoft.EtwLogging;
using Microsoft.Extensions.Options;

namespace KdSoft.EtwEvents.WebClient
{
    sealed class EventSession: IAsyncDisposable, IDisposable
    {
        readonly EtwListener.EtwListenerClient _etwClient;
        readonly EtwEventRequest _etwRequest;
        readonly EventSinkHolder _eventSinks;
        readonly AggregatingNotifier<Models.TraceSessionStates> _changeNotifier;
        readonly Channel<(EtwEventBatch, long)> _responseQueue;

        AsyncServerStreamingCall<EtwEventBatch>? _streamingCall;

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

            this._responseQueue = Channel.CreateBounded<(EtwEventBatch, long)>(new BoundedChannelOptions(optionsMonitor.CurrentValue.EventQueueCapacity) {
                AllowSynchronousContinuations = true,
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = true
            });
        }

        async Task ProcessResponseStream(CancellationToken cancelToken) {
            long sequenceNo = 0;
            var streamer = _streamingCall = _etwClient.GetEvents(_etwRequest);
            var responseStream = streamer.ResponseStream;

            try {
                while (await responseStream.MoveNext(cancelToken).ConfigureAwait(false)) {
                    // we should not call CloseAsync while still sending
                    Interlocked.MemoryBarrier();
                    if (_stopped != 0 || _streamingCall == null)
                        break;

                    var evtBatch = responseStream.Current;

                    var posted = _responseQueue.Writer.TryWrite((evtBatch, sequenceNo));
                    if (!posted) {
                        //_logger.LogInformation("Could not post trace event {eventId}.", evt.Id);
                        break;
                    }
                    sequenceNo += evtBatch.Events.Count;

                    await _changeNotifier.PostNotification().ConfigureAwait(false);
                }
            }
            catch (RpcException rex) when (rex.StatusCode == StatusCode.Cancelled) {
                // Expected, happens when we Dispose the AsyncServerStreamingCall<EtwEvent>,
                // which is the only way to stop the stream from the client.
            }
            finally {
                _responseQueue.Writer.Complete();
            }
        }

        async Task ProcessResponseQueue() {
            await foreach (var (evtBatch, sequenceNo) in _responseQueue.Reader.ReadAllAsync().ConfigureAwait(false)) {
                var success = await this._eventSinks.ProcessEventBatch(evtBatch, sequenceNo).ConfigureAwait(false);
                if (!success) {
                    await _changeNotifier.PostNotification().ConfigureAwait(false);
                }
            }
        }

        async Task RunInternal(CancellationToken cancelToken) {
            try {
                var streamTask = ProcessResponseStream(cancelToken);
                var queueTask = ProcessResponseQueue();
                await streamTask.ConfigureAwait(false);
                await queueTask.ConfigureAwait(false);
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

        public bool Run(CancellationToken cancelToken, out Task eventsTask) {
            var oldStarted = Interlocked.CompareExchange(ref _started, 1, 0);
            if (oldStarted == 1) {
                eventsTask = Task.CompletedTask;
                return false;
            }
            eventsTask = RunInternal(cancelToken);
            return true;
        }

        public async Task<bool> Stop() {
            var oldStopped = Interlocked.CompareExchange(ref _stopped, 1, 0);
            if (oldStopped == 1)
                return false;

            try {
                // not strictly necessary but help with triggering session state notifications!
                await _etwClient.StopEventsAsync(new StringValue { Value = _etwRequest.SessionName }).ResponseAsync.ConfigureAwait(false);
            }
            finally {
                // this does not always trigger ServerCallContext.CancellationToken right away!
                await DisposeAsync().ConfigureAwait(false);
            }
            return true;
        }

        // Warning: ValueTasks should not be awaited multiple times
        public ValueTask DisposeAsync() {
            Dispose();
            return default;
        }

        public void Dispose() {
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
