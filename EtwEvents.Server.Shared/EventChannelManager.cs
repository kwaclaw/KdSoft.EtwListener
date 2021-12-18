using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using KdSoft.EtwLogging;
using Microsoft.Diagnostics.Tracing;

namespace KdSoft.EtwEvents.Server
{
    public class EventChannelManager {
        readonly object _eventChannelLock = new object();
        readonly object _failedEventChannelLock = new object();
        readonly ArrayPool<(KeyValuePair<string, IEventChannel> channelEntry, ValueTask<bool> task)> _eventChannelTaskPool;

        ImmutableDictionary<string, IEventChannel> _eventChannels;
        ImmutableDictionary<string, IEventChannel> _failedEventChannels;
        CancellationTokenSource? _stoppingTokenSource;

        public IImmutableDictionary<string, IEventChannel> ActiveEventChannels => _eventChannels;
        public IImmutableDictionary<string, IEventChannel> FailedEventChannels => _failedEventChannels;

        public EventChannelManager() {
            this._eventChannels = ImmutableDictionary<string, IEventChannel>.Empty.WithComparers(StringComparer.CurrentCultureIgnoreCase);
            this._failedEventChannels = ImmutableDictionary<string, IEventChannel>.Empty.WithComparers(StringComparer.CurrentCultureIgnoreCase);
            this._eventChannelTaskPool = ArrayPool<(KeyValuePair<string, IEventChannel>, ValueTask<bool>)>.Create();
        }

        public void AddTransient(string name, IEventSink sink) {
            IEventChannel channel;
            lock (_eventChannelLock)
                lock (_failedEventChannelLock) {
                    if (_failedEventChannels.TryGetValue(name, out var failedChannel)) {
                        channel = failedChannel.Clone(sink);
                    }
                    var channel = new
        }

            /// <summary>
            /// Adds new <see cref="IEventChannel"/> to processing loop. 
            /// </summary>
            /// <param name="name">Name associated with <see cref="IEventChannel">.</param>
            /// <param name="channel">New <see cref="IEventChannel"></see> to add.</param>
            /// <remarks>The event sink must have been initialized already!.</remarks>
            public void AddEventChannel(string name, IEventChannel channel) {
            // We use a lock here to prevent race conditions, so that
            // if two concurrent updates happen, none will get lost.
            lock (_eventChannelLock) {
                this._eventChannels = this._eventChannels.Add(name, channel);
            }
        }

        /// <summary>
        /// Adds new <see cref="IEventChannel"/> to processing loop. 
        /// </summary>
        /// <param name="channelEntries">New named <see cref="IEventChannel">event sinks</see> to add.</param>
        /// <remarks>The event sinks must have been initialized already!.</remarks>
        public void AddEventChannels(IEnumerable<KeyValuePair<string, IEventChannel>> channelEntries) {
            // We use a lock here to prevent race conditions, so that
            // if two concurrent updates happen, none will get lost.
            lock (_eventChannelLock) {
                this._eventChannels = this._eventChannels.AddRange(channelEntries);
            }
        }

        /// <summary>
        /// Removes <see cref="IEventChannel"/> instance by name from processing loop.
        /// </summary>
        /// <param name="name">Name of <see cref="IEventChannel"/> instance.</param>
        /// <returns><c>true></c> if event channel was found and removed, <c>false</c> otherwise.</returns>
        public bool DeleteEventChannel(string name) {
            // We use a lock here to prevent race conditions, so that
            // if two concurrent updates happen, none will get lost.
            lock (_eventChannelLock) {
                var oldEventChannels = this._eventChannels;
                this._eventChannels = oldEventChannels.Remove(name);
                return !object.ReferenceEquals(oldEventChannels, this._eventChannels);
            }
        }

        /// <summary>
        /// Removes <see cref="IEventChannel"/> instance by name from processing loop.
        /// </summary>
        /// <param name="name">Name of <see cref="IEventChannel"/>.</param>
        /// <returns>The <see cref="IEventChannel"/> instance that was removed, or <c>null</c> if it was not found.</returns>
        /// <remarks>It is the responsibility of the caller to dispose the <see cref="IEventChannel"/> instance!</remarks>
        public IEventChannel? RemoveEventChannel(string name) {
            // We use a lock here to prevent race conditions, so that
            // if two concurrent updates happen, none will get lost.
            IEventChannel? result = null;
            lock (_eventChannelLock) {
                var oldEventChannels = this._eventChannels;
                if (oldEventChannels.TryGetValue(name, out result))
                    this._eventChannels = oldEventChannels.Remove(name);
            }
            return result;
        }

        /// <summary>
        /// Removes <see cref="IEventChannel"/> instances by name from processing loop.
        /// </summary>
        /// <param name="names">Names of <see cref="IEventChannel"/> instances to remove.</param>
        public void DeleteEventChannels(IEnumerable<string> names) {
            // We use a lock here to prevent race conditions, so that
            // if two concurrent updates happen, none will get lost.
            lock (_eventChannelLock) {
                var oldEventChannels = this._eventChannels;
                this._eventChannels = oldEventChannels.RemoveRange(names);
            }
        }

        /// <summary>
        /// Removes <see cref="IEventChannel"/> instances from processing loop.
        /// </summary>
        /// <param name="names">Names of <see cref="IEventChannel">event sinks</see>/to remove.</param>
        /// <returns>The <see cref="IEventChannel"/> instances that were removed, or an empty collection if none were found.</returns>
        /// <remarks>It is the responsibility of the caller to dispose the <see cref="IEventChannel"/> instances!</remarks>
        public IEnumerable<IEventChannel> RemoveEventChannels(IEnumerable<string> names) {
            if (names == null)
                throw new ArgumentNullException(nameof(names));
            // We use a lock here to prevent race conditions, so that
            // if two concurrent updates happen, none will get lost.
            var oldChannels = new List<IEventChannel>();
            lock (_eventChannelLock) {
                var oldEventChannels = this._eventChannels;
                foreach (var name in names) {
                    if (oldEventChannels.TryGetValue(name, out var oldSink))
                        oldChannels.Add(oldSink);
                }
                this._eventChannels = oldEventChannels.RemoveRange(names);
            }
            return oldChannels;
        }

        /// <summary>
        /// Removes named entries from list of failed sinks.
        /// </summary>
        /// <param name="names">Names of failed <see cref="IEventChannel">event sinks</see> to remove.</param>
        /// <returns>The names of the failed <see cref="IEventChannel">event sinks</see> that were removed, or an empty collection if none were found.</returns>
        public IEnumerable<string> RemoveFailedEventChannels(IEnumerable<string> names) {
            if (names == null)
                throw new ArgumentNullException(nameof(names));
            // We use a lock here to prevent race conditions, so that
            // if two concurrent updates happen, none will get lost.
            var oldChannels = new List<string>();
            lock (_eventChannelLock) {
                var oldEventChannels = this._failedEventChannels;
                foreach (var name in names) {
                    if (oldEventChannels.ContainsKey(name))
                        oldChannels.Add(name);
                }
                this._failedEventChannels = oldEventChannels.RemoveRange(names);
            }
            return oldChannels;
        }

        public (IImmutableDictionary<string, IEventChannel> active, IImmutableDictionary<string, (string, Exception)> failed) ClearEventChannels() {
            lock (_eventChannelLock) {
                var active = this._eventChannels;
                var failed = this._failedEventChannels;
                this._eventChannels = ImmutableDictionary<string, IEventChannel>.Empty.WithComparers(StringComparer.CurrentCultureIgnoreCase);
                this._failedEventChannels = ImmutableDictionary<string, (string, Exception)>.Empty.WithComparers(StringComparer.CurrentCultureIgnoreCase);
                return (active, failed);
            }
        }

        // Note: Disposes of IEventSinkChannel instance
        public async Task<bool> CloseEventChannel(string name, IEventChannel channel) {
            bool removed = DeleteEventChannel(name);
            // must not throw!
            await channel.DisposeAsync().ConfigureAwait(false);
            return removed;
        }

        // Note: Disposes of failed IEventSinkChannel instance
        public async Task HandleFailedEventChannel(string name, IEventChannel channel, Exception ex) {
            var removed = await CloseEventChannel(name, channel).ConfigureAwait(false);
            if (removed) {
                var failedType = channel.GetType().Name;
                lock (_failedEventChannelLock) {
                    this._failedEventChannels = this._failedEventChannels.SetItem(name, (failedType, ex));
                }
            }
        }

        async Task<bool> CheckEventSinkWriteTasks((KeyValuePair<string, IEventChannel> sinkEntry, ValueTask<bool> task)[] taskList, int taskCount) {
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
                    await HandleFailedEventChannel(sinkEntry.Key, sinkEntry.Value, ex).ConfigureAwait(false);
                    result = false;
                }
            }
            return result;
        }

        public void PostEvent(TraceEvent evt) {
            var eventChannels = this._eventChannels;
            foreach (var entry in eventChannels) {
                entry.Value.PostEvent(evt);
            }
        }

        public void StartProcessing(CancellationToken stoppingToken) {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var oldCts = Interlocked.Exchange(ref _stoppingTokenSource, cts);
            if (oldCts != null) {
                oldCts.Cancel();
                oldCts.Dispose();
            }
            var linkedToken = cts.Token;
        }

        public Task StopProcessing() {
            _stoppingTokenSource?.Cancel();
            return default;
        }

        //public Task ProcessBatches(CancellationToken stoppingToken) {
        //    //TODO completes when all channels have completed
        //    //TODO what if all channels complete but we have not stopped posting events?
        //    //TODO need to parts:
        //    //  1) start processing, channels can be added or removed dynamically
        //    //  2) when posting stops, no more channels can be added, we await the current channels
        //    return Task.Run(() => { });
        //}
    }
}
