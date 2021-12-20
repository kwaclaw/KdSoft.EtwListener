using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using KdSoft.EtwLogging;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Extensions.Logging;

namespace KdSoft.EtwEvents.Server
{
    public class EventChannelManager {
        readonly ILoggerFactory _loggerFactory;
        readonly object _eventChannelLock = new object();
        readonly object _failedEventChannelLock = new object();
        readonly ArrayPool<(KeyValuePair<string, EventChannel> channelEntry, ValueTask<bool> task)> _eventChannelTaskPool;

        ImmutableDictionary<string, EventChannel> _eventChannels;
        ImmutableDictionary<string, EventChannel> _failedEventChannels;
        CancellationTokenSource? _stoppingTokenSource;

        public IImmutableDictionary<string, EventChannel> ActiveEventChannels => _eventChannels;
        public IImmutableDictionary<string, EventChannel> FailedEventChannels => _failedEventChannels;

        public EventChannelManager(ILoggerFactory loggerFactory) {
            this._loggerFactory = loggerFactory;
            this._eventChannels = ImmutableDictionary<string, EventChannel>.Empty.WithComparers(StringComparer.CurrentCultureIgnoreCase);
            this._failedEventChannels = ImmutableDictionary<string, EventChannel>.Empty.WithComparers(StringComparer.CurrentCultureIgnoreCase);
            this._eventChannelTaskPool = ArrayPool<(KeyValuePair<string, EventChannel>, ValueTask<bool>)>.Create();
        }

        /// <summary>
        /// Adds new <see cref="EventChannel"/> to set of active channels.
        /// </summary>
        /// <typeparam name="T">(Sub)Type of EventChannel.</typeparam>
        /// <param name="name">Name of EventChannel.</param>
        /// <param name="sink">Event sink that the channel has to use.</param>
        /// <param name="createChannel">Callback to create a new EventChannel instance, or clone from an existing one.</param>
        /// <returns>Newly added EventChannel.</returns>
        /// <exception cref="InvalidOperationException"></exception>
        public T AddChannel<T>(string name, IEventSink sink, Func<IEventSink, T?, T> createChannel) where T: EventChannel {
            T newChannel;
            lock (_eventChannelLock) {
                if (_eventChannels.ContainsKey(name)) {
                    throw new InvalidOperationException($"Event sink {name} already exists.");
                }
                lock (_failedEventChannelLock) {
                    if (_failedEventChannels.TryGetValue(name, out var eventChannel)) {
                        if (eventChannel.GetType() != typeof(T))
                            eventChannel = null;
                    }
                    newChannel = createChannel(sink, (T?)eventChannel);
                    _failedEventChannels.Remove(name);
                    _eventChannels = _eventChannels.Add(name, newChannel);
                }
            }
            return newChannel;
        }

        //TODO still need to start processing on the new channel, and need to set up failure/closing handling
        // Remove: means remove channel and close event sink
        // Channel failure means: remove channel, close/dispose event sink
        // Event sink RunTask completion means: remove channel, close/dispose event sink if necessary


        /// <summary>
        /// Removes <see cref="IEventChannel"/> instance by name from processing loop.
        /// </summary>
        /// <param name="name">Name of <see cref="IEventChannel"/>.</param>
        /// <returns>The <see cref="IEventChannel"/> instance that was removed, or <c>null</c> if it was not found.</returns>
        /// <remarks>It is the responsibility of the caller to dispose the <see cref="IEventChannel"/> instance!</remarks>
        public EventChannel? RemoveChannel(string name) {
            // We use a lock here to prevent race conditions, so that
            // if two concurrent updates happen, none will get lost.
            EventChannel? result = null;
            lock (_eventChannelLock) {
                var oldEventChannels = this._eventChannels;
                if (oldEventChannels.TryGetValue(name, out result))
                    this._eventChannels = oldEventChannels.Remove(name);
            }
            return result;
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

        public (IImmutableDictionary<string, EventChannel> active, IImmutableDictionary<string, EventChannel> failed) ClearEventChannels() {
            lock (_eventChannelLock) {
                var active = this._eventChannels;
                var failed = this._failedEventChannels;
                this._eventChannels = ImmutableDictionary<string, EventChannel>.Empty.WithComparers(StringComparer.CurrentCultureIgnoreCase);
                this._failedEventChannels = ImmutableDictionary<string, EventChannel>.Empty.WithComparers(StringComparer.CurrentCultureIgnoreCase);
                return (active, failed);
            }
        }

        // Note: Disposes of IEventSinkChannel instance
        public async Task<bool> CloseEventChannel(string name, EventChannel channel) {
            var oldChannel = RemoveChannel(name);
            // must not throw!
            await channel.DisposeAsync().ConfigureAwait(false);
            return object.ReferenceEquals(oldChannel, channel);
        }

        // Note: Disposes of failed IEventSinkChannel instance
        public async Task HandleFailedEventChannel(string name, EventChannel channel, Exception ex) {
            var removed = await CloseEventChannel(name, channel).ConfigureAwait(false);
            if (removed) {
                lock (_failedEventChannelLock) {
                    this._failedEventChannels = this._failedEventChannels.SetItem(name, channel);
                }
            }
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
