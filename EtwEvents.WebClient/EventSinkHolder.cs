using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using KdSoft.EtwLogging;

namespace EtwEvents.WebClient
{
    public class EventSinkHolder
    {
        readonly object _eventSinkLock = new object();
        readonly object _failedEventSinkLock = new object();
        readonly ArrayPool<ValueTask<bool>> _taskArrayPool;

        ImmutableList<IEventSink> _eventSinks;
        ImmutableList<(IEventSink, Exception?)> _failedEventSinks;

        public ImmutableList<IEventSink> ActiveEventSinks => _eventSinks;
        public ImmutableList<(IEventSink, Exception?)> FailedEventSinks => _failedEventSinks;

        public EventSinkHolder() {
            this._eventSinks = ImmutableList<IEventSink>.Empty;
            this._failedEventSinks = ImmutableList<(IEventSink, Exception?)>.Empty;
            this._taskArrayPool = ArrayPool<ValueTask<bool>>.Create();
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
            }
        }

        /// <summary>
        /// Adds new <see cref="IEventSink"/> to processing loop. 
        /// </summary>
        /// <param name="sinks">New <see cref="IEventSink">event sinks</see> to add.</param>
        /// <remarks>The event sinks must have been initialized already!.</remarks>
        public void AddEventSinks(IEnumerable<IEventSink> sinks) {
            // We use a lock here to prevent race conditions, so that
            // if two concurrent updates happen, none will get lost.
            lock (_eventSinkLock) {
                this._eventSinks = this._eventSinks.AddRange(sinks);
            }
        }

        /// <summary>
        /// Removes given <see cref="IEventSink"/> instance from processing loop.
        /// </summary>
        /// <param name="sink"><see cref="IEventSink"/> instance to remove.</param>
        /// <returns><c>true></c> if event sink was found and removed, <c>false</c> otherwise.</returns>
        public bool RemoveEventSink(IEventSink sink) {
            // We use a lock here to prevent race conditions, so that
            // if two concurrent updates happen, none will get lost.
            lock (_eventSinkLock) {
                var oldEventSinks = this._eventSinks;
                this._eventSinks = oldEventSinks.Remove(sink);
                return !object.ReferenceEquals(oldEventSinks, this._eventSinks);
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
        /// Removes <see cref="IEventSink"/> instances from processing loop.
        /// </summary>
        /// <param name="ids">Identifiers of <see cref="IEventSink">event sinks</see>/to remove.</param>
        /// <returns>The <see cref="IEventSink"/> instances that were removed, or an empty collection if none were found.</returns>
        /// <remarks>It is the responsibility of the caller to dispose the <see cref="IEventSink"/> instances!</remarks>
        public IEnumerable<IEventSink> RemoveEventSinks(IEnumerable<string> ids) {
            if (ids == null)
                throw new ArgumentNullException(nameof(ids));
            // We use a lock here to prevent race conditions, so that
            // if two concurrent updates happen, none will get lost.
            var oldSinks = new List<IEventSink>();
            lock (_eventSinkLock) {
                var eventSinks = this._eventSinks;
                foreach (var id in ids) {
                    var oldSink = eventSinks.Find(sink => id.Equals(sink.Id, StringComparison.Ordinal));
                    if (oldSink != null)
                        oldSinks.Add(oldSink);
                }
                this._eventSinks = eventSinks.RemoveRange(oldSinks);
            }
            return oldSinks;
        }

        /// <summary>
        /// Removes given <see cref="IEventSink"/> instance from processing loop.
        /// </summary>
        /// <param name="sinks"><see cref="IEventSink"/> instances to remove.</param>
        public void RemoveEventSinks(IEnumerable<IEventSink> sinks) {
            // We use a lock here to prevent race conditions, so that
            // if two concurrent updates happen, none will get lost.
            lock (_eventSinkLock) {
                var oldEventSinks = this._eventSinks;
                this._eventSinks = oldEventSinks.RemoveRange(sinks);
            }
        }

        public (ImmutableList<IEventSink> active, ImmutableList<(IEventSink, Exception?)> failed) ClearEventSInks() {
            ImmutableList<IEventSink> active;
            ImmutableList<(IEventSink, Exception?)> failed;
            lock (_eventSinkLock) {
                active = this._eventSinks;
                failed = this._failedEventSinks;
                this._eventSinks = ImmutableList<IEventSink>.Empty;
                this._failedEventSinks = ImmutableList<(IEventSink, Exception?)>.Empty;
            }
            return (active, failed);
        }

        void HandleFailedEventSink(IEventSink failedSink, Exception? ex) {
            if (RemoveEventSink(failedSink))
                //TODO should we dospose the event sink here or somehow notify the owner of this session
                lock (_failedEventSinkLock) {
                    this._failedEventSinks = this._failedEventSinks.Add((failedSink, ex));
                }
        }

        public async Task ProcessEvent(EtwEvent? evt, long sequenceNo) {
            // We do not use a lock here because reference field access is atomic
            // and we do not exactly care which version of the field value we get. 
            var eventSinks = this._eventSinks;

            var taskList = this._taskArrayPool.Rent(eventSinks.Count);
            try {
                if (evt == null) {
                    for (int indx = 0; indx < eventSinks.Count; indx++) {
                        var writeTask = eventSinks[indx].FlushAsync();
                        taskList[indx] = writeTask;
                    }
                }
                else {
                    for (int indx = 0; indx < eventSinks.Count; indx++) {
                        var writeTask = eventSinks[indx].WriteAsync(evt, sequenceNo);
                        taskList[indx] = writeTask;
                    }
                }

                for (int indx = 0; indx < eventSinks.Count; indx++) {
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
            }
            finally {
                this._taskArrayPool.Return(taskList);
            }
        }
    }
}
