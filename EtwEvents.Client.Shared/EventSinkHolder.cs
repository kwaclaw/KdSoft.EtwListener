using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.Loader;
using System.Threading.Tasks;
using KdSoft.EtwLogging;

namespace KdSoft.EtwEvents.Client
{
    public class EventSinkHolder
    {
        readonly object _eventSinkLock = new object();
        readonly object _failedEventSinkLock = new object();
        readonly ArrayPool<(KeyValuePair<string, IEventSink> sinkEntry, ValueTask<bool> task)> _eventSinkTaskPool;


        ImmutableDictionary<string, IEventSink> _eventSinks;
        ImmutableDictionary<string, (string, Exception)> _failedEventSinks;

        public IImmutableDictionary<string, IEventSink> ActiveEventSinks => _eventSinks;
        public IImmutableDictionary<string, (string sinkType, Exception error)> FailedEventSinks => _failedEventSinks;

        public EventSinkHolder() {
            this._eventSinks = ImmutableDictionary<string, IEventSink>.Empty.WithComparers(StringComparer.CurrentCultureIgnoreCase);
            this._failedEventSinks = ImmutableDictionary<string, (string, Exception)>.Empty.WithComparers(StringComparer.CurrentCultureIgnoreCase);
            this._eventSinkTaskPool = ArrayPool<(KeyValuePair<string, IEventSink>, ValueTask<bool>)>.Create();
        }

        /// <summary>
        /// Adds new <see cref="IEventSink"/> to processing loop. 
        /// </summary>
        /// <param name="name">Name associated with <see cref="IEventSink">.</param>
        /// <param name="sink">New <see cref="IEventSink"></see> to add.</param>
        /// <remarks>The event sink must have been initialized already!.</remarks>
        public void AddEventSink(string name, IEventSink sink) {
            // We use a lock here to prevent race conditions, so that
            // if two concurrent updates happen, none will get lost.
            lock (_eventSinkLock) {
                this._eventSinks = this._eventSinks.Add(name, sink);
            }
        }

        /// <summary>
        /// Adds new <see cref="IEventSink"/> to processing loop. 
        /// </summary>
        /// <param name="sinkEntries">New named <see cref="IEventSink">event sinks</see> to add.</param>
        /// <remarks>The event sinks must have been initialized already!.</remarks>
        public void AddEventSinks(IEnumerable<KeyValuePair<string, IEventSink>> sinkEntries) {
            // We use a lock here to prevent race conditions, so that
            // if two concurrent updates happen, none will get lost.
            lock (_eventSinkLock) {
                this._eventSinks = this._eventSinks.AddRange(sinkEntries);
            }
        }

        /// <summary>
        /// Removes <see cref="IEventSink"/> instance by name from processing loop.
        /// </summary>
        /// <param name="name">Name of <see cref="IEventSink"/> instance.</param>
        /// <returns><c>true></c> if event sink was found and removed, <c>false</c> otherwise.</returns>
        public bool DeleteEventSink(string name) {
            // We use a lock here to prevent race conditions, so that
            // if two concurrent updates happen, none will get lost.
            lock (_eventSinkLock) {
                var oldEventSinks = this._eventSinks;
                this._eventSinks = oldEventSinks.Remove(name);
                return !object.ReferenceEquals(oldEventSinks, this._eventSinks);
            }
        }

        /// <summary>
        /// Removes <see cref="IEventSink"/> instance by name from processing loop.
        /// </summary>
        /// <param name="name">Name of <see cref="IEventSink"/>.</param>
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
        /// Removes <see cref="IEventSink"/> instances by name from processing loop.
        /// </summary>
        /// <param name="names">Names of <see cref="IEventSink"/> instances to remove.</param>
        public void DeleteEventSinks(IEnumerable<string> names) {
            // We use a lock here to prevent race conditions, so that
            // if two concurrent updates happen, none will get lost.
            lock (_eventSinkLock) {
                var oldEventSinks = this._eventSinks;
                this._eventSinks = oldEventSinks.RemoveRange(names);
            }
        }

        /// <summary>
        /// Removes <see cref="IEventSink"/> instances from processing loop.
        /// </summary>
        /// <param name="names">Names of <see cref="IEventSink">event sinks</see>/to remove.</param>
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
        /// <param name="names">Names of failed <see cref="IEventSink">event sinks</see> to remove.</param>
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

        public (IImmutableDictionary<string, IEventSink> active, IImmutableDictionary<string, (string, Exception)> failed) ClearEventSinks() {
            lock (_eventSinkLock) {
                var active = this._eventSinks;
                var failed = this._failedEventSinks;
                this._eventSinks = ImmutableDictionary<string, IEventSink>.Empty.WithComparers(StringComparer.CurrentCultureIgnoreCase);
                this._failedEventSinks = ImmutableDictionary<string, (string, Exception)>.Empty.WithComparers(StringComparer.CurrentCultureIgnoreCase);
                return (active, failed);
            }
        }

        // unload only once! otherwise we get a System.ExecutionEngineException
        void UnloadEventSink(string name, IEventSink eventSink) {
            var sinkAssembly = eventSink.GetType().Assembly;
            if (sinkAssembly != null) {
                var loadContext = AssemblyLoadContext.GetLoadContext(sinkAssembly);
                if (loadContext != null && loadContext.IsCollectible)
                    loadContext.Unload();
            }
        }

        public async Task CloseEventSink(string name, IEventSink sink) {
            bool removed = DeleteEventSink(name);
            try {
                await sink.DisposeAsync().ConfigureAwait(false);
            }
            finally {
                if (removed)
                    UnloadEventSink(name, sink);
            }
        }

        // Note: Disposes of failed IEventSink instance
        public async Task HandleFailedEventSink(string name, IEventSink sink, Exception ex) {
            bool removed = DeleteEventSink(name);
            try {
                await sink.DisposeAsync().ConfigureAwait(false);
                if (removed) {
                    var failedType = sink.GetType().Name;
                    lock (_failedEventSinkLock) {
                        this._failedEventSinks = this._failedEventSinks.SetItem(name, (failedType, ex));
                    }
                }
            }
            finally {
                if (removed)
                    UnloadEventSink(name, sink);
            }
        }

        async Task<bool> CheckEventSinkWriteTasks((KeyValuePair<string, IEventSink> sinkEntry, ValueTask<bool> task)[] taskList, int taskCount) {
            var result = true;
            for (int indx = 0; indx < taskCount; indx++) {
                var (sinkEntry, task) = taskList[indx];
                try {
                    var success = await task.ConfigureAwait(false);
                    // we assume that the IEventSink.RunTask is now complete and the event sink will be closed
                    if (!success) {
                        result = false;
                    }
                }
                catch (Exception ex) {
                    await HandleFailedEventSink(sinkEntry.Key, sinkEntry.Value, ex).ConfigureAwait(false);
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
                    taskList[indx++] = (entry, entry.Value.WriteAsync(evtBatch, sequenceNo));
                }
                result = await CheckEventSinkWriteTasks(taskList, eventSinks.Count).ConfigureAwait(false);
                if (!result)
                    return result;

                // WriteAsync() and FlushAsync() must not be called concurrently on the same event sink
                indx = 0;
                foreach (var entry in eventSinks) {
                    taskList[indx++] = (entry, entry.Value.FlushAsync());
                }
                result = await CheckEventSinkWriteTasks(taskList, eventSinks.Count).ConfigureAwait(false);
            }
            finally {
                this._eventSinkTaskPool.Return(taskList);
            }
            return result;
        }
    }
}
