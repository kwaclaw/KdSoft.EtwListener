using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
        readonly EtwEventRequest _etwRequest;
        readonly Timer _flushTimer;
        readonly object _eventSinkLock = new object();
        readonly object _failedEventSinkLock = new object();

        ImmutableList<IEventSink> _eventSinks;
        AsyncServerStreamingCall<EtwEvent>? _streamingCall;
        int _pushFrequencyMillisecs;
        IDisposable _pushFrequencyMonitor;
        ActionBlock<(EtwEvent?, long)> _jobQueue;

        int _started = 0;
        int _stopped = 0;

        ImmutableList<(IEventSink, Exception?)> _failedEventSinks;
        public ImmutableList<(IEventSink, Exception?)> FailedEventSinks => _failedEventSinks;

        public EventSession(
            EtwListener.EtwListenerClient etwClient,
            EtwEventRequest etwRequest,
            IOptionsMonitor<EventSessionOptions> optionsMonitor
        ) {
            this._etwClient = etwClient;
            this._etwRequest = etwRequest;
            this._eventSinks = ImmutableList<IEventSink>.Empty;
            this._failedEventSinks = ImmutableList<(IEventSink, Exception?)>.Empty;

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

        /// <summary>
        /// Adds new <see cref="IEventSink"/> to processing loop. 
        /// </summary>
        /// <param name="sink">New <see cref="IEventSink"></see> to add.</param>
        /// <remarks>The event sink must have been initialized already!.</remarks>
        public void AddEventSink(IEventSink sink) {
            // We use a lock here to prevent race conditions, so that
            // if two concurrent updates happen, none will get lost.
            lock (_eventSinkLock) {
                this._eventSinks = this._eventSinks.Add(sink);
                Interlocked.MemoryBarrier();
            }
        }

        /// <summary>
        /// Removes <see cref="IEventSink"/> instance from processing loop.
        /// </summary>
        /// <param name="id">Identifier of <see cref="IEventSink"/> to remove.</param>
        /// <returns>The <see cref="IEventSink"/> instance that was removed, or <c>null</c> if it was not found.</returns>
        /// <remarks>It is the responsibility of the caller to dispose the <see cref="IEventSink"/> instance!</remarks>
        public IEventSink? RemoveEventSink(string id) {
            // We use a lock here to prevent race conditions, so that
            // if two concurrent updates happen, none will get lost.
            lock (_eventSinkLock) {
                var eventSinks = this._eventSinks;
                var sinkIndex = eventSinks.FindIndex(sink => id.Equals(sink.Id, StringComparison.Ordinal));
                if (sinkIndex < 0)
                    return null;
                var oldSink = eventSinks[sinkIndex];
                this._eventSinks = eventSinks.RemoveAt(sinkIndex);
                return oldSink;
            }
        }

        /// <summary>
        /// Removes given <see cref="IEventSink"/> instance from processing loop.
        /// </summary>
        /// <param name="sink"><see cref="IEventSink"/> instance to remove.</param>
        /// <returns><c>true</c> if instance was found and removed, <c>false</c> otherwise.</returns>
        public bool RemoveEventSink(IEventSink sink) {
            // We use a lock here to prevent race conditions, so that
            // if two concurrent updates happen, none will get lost.
            lock (_eventSinkLock) {
                var eventSinks = this._eventSinks;
                this._eventSinks = eventSinks.Remove(sink);
                return !object.ReferenceEquals(eventSinks, this._eventSinks);
            }
        }

        //async Task WriteResponse(IEventSink eventSink, EtwEvent? evt, long sequenceNo) {
        //    bool success;
        //    if (evt == null) {
        //        success = await eventSink.FlushAsync().ConfigureAwait(false);
        //        if (success) {
        //            var pushFrequency = this._pushFrequencyMillisecs;
        //            this._flushTimer?.Change(pushFrequency, Timeout.Infinite);
        //        }
        //    }
        //    else {
        //        success = await eventSink.WriteAsync(evt, sequenceNo).ConfigureAwait(false);
        //    }
        //}

        void HandleFailedEventSink(IEventSink failedSink, Exception? ex) {
            if (RemoveEventSink(failedSink))
                lock (_failedEventSinkLock) {
                    this._failedEventSinks = this._failedEventSinks.Add((failedSink, ex));
                }
        }

        async Task ProcessResponse((EtwEvent? evt, long sequenceNo) args) {
            var taskList = new List<ValueTask<bool>>();

            // We do not use a lock here because reference field access is atomic
            // and we do not exactly care which version of the field value we get. 
            var eventSinks = this._eventSinks;

            if (args.evt == null) {
                for (int indx = 0; indx < eventSinks.Count; indx++) {
                    var writeTask = eventSinks[indx].FlushAsync();
                    taskList.Add(writeTask);
                }
            }
            else {
                for (int indx = 0; indx < eventSinks.Count; indx++) {
                    var writeTask = eventSinks[indx].WriteAsync(args.evt, args.sequenceNo);
                    taskList.Add(writeTask);
                }
            }

            for (int indx = 0; indx < taskList.Count; indx++) {
                try {
                    var success = await taskList[indx].ConfigureAwait(false);
                    if (!success) {
                        HandleFailedEventSink(eventSinks[indx], null);
                    }
                }
                catch (Exception ex) {
                    HandleFailedEventSink(eventSinks[indx], ex);
                }
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

        public async Task<bool> Run(CancellationToken cancelToken, params IEventSink[] eventSinks) {
            var oldStarted = Interlocked.CompareExchange(ref _started, 1, 0);
            if (oldStarted == 1)
                return false;

            lock(_eventSinkLock) {
                this._eventSinks = this._eventSinks.AddRange(eventSinks);
            }

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

            lock (_eventSinkLock) {
                this._eventSinks = ImmutableList<IEventSink>.Empty;
            }

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
