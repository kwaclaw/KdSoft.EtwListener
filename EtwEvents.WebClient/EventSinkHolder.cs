using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using KdSoft.EtwEvents.Client.Shared;
using KdSoft.EtwLogging;

namespace KdSoft.EtwEvents.WebClient
{
    class EventSinkHolder
    {
        readonly object _eventSinkLock = new object();
        readonly object _failedEventSinkLock = new object();
        readonly ArrayPool<(IEventSink sink, ValueTask<bool> task)> _eventSinkTaskPool;


        ImmutableDictionary<string, IEventSink> _eventSinks;
        ImmutableDictionary<string, (string, Exception)> _failedEventSinks;

        public IImmutableDictionary<string, IEventSink> ActiveEventSinks => _eventSinks;
        public IImmutableDictionary<string, (string sinkType, Exception error)> FailedEventSinks => _failedEventSinks;

        public EventSinkHolder() {
            this._eventSinks = ImmutableDictionary<string, IEventSink>.Empty.WithComparers(StringComparer.CurrentCultureIgnoreCase);
            this._failedEventSinks = ImmutableDictionary<string, (string, Exception)>.Empty.WithComparers(StringComparer.CurrentCultureIgnoreCase);
            this._eventSinkTaskPool = ArrayPool<(IEventSink, ValueTask<bool>)>.Create();
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
                this._eventSinks = this._eventSinks.Add(sink.Name, sink);
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
                this._eventSinks = this._eventSinks.AddRange(sinks.Select(sink => new KeyValuePair<string, IEventSink>(sink.Name, sink)));
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
                this._eventSinks = oldEventSinks.Remove(sink.Name);
                return !object.ReferenceEquals(oldEventSinks, this._eventSinks);
            }
        }

        /// <summary>
        /// Removes <see cref="IEventSink"/> instance from processing loop.
        /// </summary>
        /// <param name="name">Identifier of <see cref="IEventSink"/> to remove.</param>
        /// <returns>The <see cref="IEventSink"/> instance that was removed, or <c>null</c> if it was not found.</returns>
        /// <remarks>It is the responsibility of the caller to dispose the <see cref="IEventSink"/> instance!</remarks>
        public IEventSink? RemoveEventSink(string name) {
            // We use a lock here to prevent race conditions, so that
            // if two concurrent updates happen, none will get lost.
            IEventSink? result = null;
            lock (_eventSinkLock) {
                var oldEventSinks = this._eventSinks;
                if (oldEventSinks.TryGetValue(name, out result))
                    this._eventSinks = oldEventSinks.Remove(name);
            }
            return result;
        }

        /// <summary>
        /// Removes <see cref="IEventSink"/> instances from processing loop.
        /// </summary>
        /// <param name="names">Identifiers of <see cref="IEventSink">event sinks</see>/to remove.</param>
        /// <returns>The <see cref="IEventSink"/> instances that were removed, or an empty collection if none were found.</returns>
        /// <remarks>It is the responsibility of the caller to dispose the <see cref="IEventSink"/> instances!</remarks>
        public IEnumerable<IEventSink> RemoveEventSinks(IEnumerable<string> names) {
            if (names == null)
                throw new ArgumentNullException(nameof(names));
            // We use a lock here to prevent race conditions, so that
            // if two concurrent updates happen, none will get lost.
            var oldSinks = new List<IEventSink>();
            lock (_eventSinkLock) {
                var oldEventSinks = this._eventSinks;
                foreach (var name in names) {
                    if (oldEventSinks.TryGetValue(name, out var oldSink))
                        oldSinks.Add(oldSink);
                }
                this._eventSinks = oldEventSinks.RemoveRange(names);
            }
            return oldSinks;
        }

        /// <summary>
        /// Removes named entries from list of failed sinks.
        /// </summary>
        /// <param name="names">Identifiers of failed <see cref="IEventSink">event sinks</see> to remove.</param>
        /// <returns>The names of the failed <see cref="IEventSink">event sinks</see> that were removed, or an empty collection if none were found.</returns>
        public IEnumerable<string> RemoveFailedEventSinks(IEnumerable<string> names) {
            if (names == null)
                throw new ArgumentNullException(nameof(names));
            // We use a lock here to prevent race conditions, so that
            // if two concurrent updates happen, none will get lost.
            var oldSinks = new List<string>();
            lock (_eventSinkLock) {
                var oldEventSinks = this._failedEventSinks;
                foreach (var name in names) {
                    if (oldEventSinks.ContainsKey(name))
                        oldSinks.Add(name);
                }
                this._failedEventSinks = oldEventSinks.RemoveRange(names);
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
                this._eventSinks = oldEventSinks.RemoveRange(sinks.Select(sink => sink.Name));
            }
        }

        public (IImmutableDictionary<string, IEventSink> active, IImmutableDictionary<string, (string, Exception)> failed) ClearEventSinks() {
            lock (_eventSinkLock) {
                var active = this._eventSinks;
                var failed = this._failedEventSinks;
                this._eventSinks = ImmutableDictionary<string, IEventSink>.Empty.WithComparers(StringComparer.CurrentCultureIgnoreCase);
                this._failedEventSinks = ImmutableDictionary<string, (string, Exception)>.Empty.WithComparers(StringComparer.CurrentCultureIgnoreCase);
                return (active, failed);
            }
        }

        // Note: Disposes of failed IEventSink instance
        async Task HandleFailedEventSink(IEventSink failedSink, Exception ex) {
            var failedName = failedSink.Name;
            var failedType = failedSink.GetType().Name;

            bool removed = RemoveEventSink(failedSink);
            try { await failedSink.DisposeAsync().ConfigureAwait(false); }
            catch { }

            if (removed) {
                lock (_failedEventSinkLock) {
                    this._failedEventSinks = this._failedEventSinks.SetItem(failedName, (failedType, ex));
                }
            }
        }

        async Task<bool> CheckEventSinkTasks((IEventSink sink, ValueTask<bool> task)[] taskList, int taskCount) {
            var result = true;
            for (int indx = 0; indx < taskCount; indx++) {
                var (sink, task) = taskList[indx];
                try {
                    var success = await task.ConfigureAwait(false);
                    if (!success) {
                        RemoveEventSink(sink);
                        result = false;
                    }
                }
                catch (Exception ex) {
                    await HandleFailedEventSink(sink, ex).ConfigureAwait(false);
                    result = false;
                }
            }
            return result;
        }

        /// <summary>
        /// Write a batch of ETW events to each event sink, and flush them afterwards.
        /// </summary>
        /// <param name="evtBatch"></param>
        /// <param name="sequenceNo"></param>
        /// <returns></returns>
        public async Task<bool> ProcessEventBatch(EtwEventBatch evtBatch, long sequenceNo) {
            // We do not use a lock here because reference field access is atomic
            // and we do not exactly care which version of the field value we get. 
            var eventSinks = this._eventSinks;
            var result = true;

            var taskList = this._eventSinkTaskPool.Rent(eventSinks.Count);
            try {
                int indx = 0;
                foreach (var entry in eventSinks) {
                    taskList[indx++] = (entry.Value, entry.Value.WriteAsync(evtBatch, sequenceNo));
                }
                result = await CheckEventSinkTasks(taskList, eventSinks.Count).ConfigureAwait(false);
                if (!result)
                    return result;

                // WriteAsync() and FlushAsync() must not be called concurrently on the same event sink
                indx = 0;
                foreach (var entry in eventSinks) {
                    taskList[indx++] = (entry.Value, entry.Value.FlushAsync());
                }
                result = await CheckEventSinkTasks(taskList, eventSinks.Count).ConfigureAwait(false);
            }
            finally {
                this._eventSinkTaskPool.Return(taskList);
            }
            return result;
        }
    }
}
