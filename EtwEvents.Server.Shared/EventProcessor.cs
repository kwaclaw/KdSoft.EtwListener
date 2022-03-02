using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing;

namespace KdSoft.EtwEvents.Server
{
    public class EventProcessor: IAsyncDisposable
    {
        readonly object _eventChannelLock = new object();

        ImmutableDictionary<string, EventChannel> _activeEventChannels;
        ImmutableDictionary<string, EventChannel> _closedEventChannels;
        ImmutableDictionary<string, EventChannel> _failedEventChannels;
        CancellationTokenSource _stoppingTokenSource;
        bool _stopping;
        bool _running;

        public IImmutableDictionary<string, EventChannel> ActiveEventChannels => _activeEventChannels;
        public IImmutableDictionary<string, EventChannel> ClosedEventChannels => _closedEventChannels;
        public IImmutableDictionary<string, EventChannel> FailedEventChannels => _failedEventChannels;

        public EventProcessor() {
            this._activeEventChannels = ImmutableDictionary<string, EventChannel>.Empty.WithComparers(StringComparer.CurrentCultureIgnoreCase);
            this._closedEventChannels = ImmutableDictionary<string, EventChannel>.Empty.WithComparers(StringComparer.CurrentCultureIgnoreCase);
            this._failedEventChannels = ImmutableDictionary<string, EventChannel>.Empty.WithComparers(StringComparer.CurrentCultureIgnoreCase);
            _stoppingTokenSource = new CancellationTokenSource();
        }

        bool TryStartChannel(string name, EventChannel channel, CancellationToken stopToken) {
            var result = channel.RunTask == null;
            if (!result)
                return result;

            async void HandleRunTaskCompletion(Task runTask) {
                try {
                    EventChannel? alreadyClosedChannel = null;
                    lock (_eventChannelLock) {
                        _activeEventChannels = _activeEventChannels.Remove(name);
                        if (runTask.IsFaulted) {
                            _failedEventChannels = _failedEventChannels.SetItem(name, channel);
                            // it seems we need to re-throw the exception to propagate it to the next task 
                            runTask.GetAwaiter().GetResult();
                        }
                        else {
                            _closedEventChannels.TryGetValue(name, out alreadyClosedChannel);
                            _closedEventChannels = _closedEventChannels.SetItem(name, channel);
                        }
                    }
                    if (alreadyClosedChannel != null) {
                        await alreadyClosedChannel.DisposeAsync().ConfigureAwait(false);
                    }
                }
                catch {
                    // ignore
                }
            }
            channel.StartProcessing(HandleRunTaskCompletion, stopToken);

            return result;
        }

        /// <summary>
        /// Creates new <see cref="EventChannel"/> for event sink and adds it to set of active channels.
        /// </summary>
        /// <typeparam name="T">(Sub)Type of EventChannel.</typeparam>
        /// <param name="name">Name of EventChannel.</param>
        /// <param name="sink">Event sink that the channel has to use.</param>
        /// <param name="createChannel">Callback to create a new EventChannel instance.</param>
        /// <returns>Newly added EventChannel.</returns>
        /// <exception cref="InvalidOperationException"></exception>
        /// <remarks>To remove an event channel, simply dispose it.</remarks>
        public T AddChannel<T>(string name, IEventSink sink, Func<IEventSink, T> createChannel) where T : EventChannel {
            T newChannel;
            lock (_eventChannelLock) {
                if (_stopping) {
                    throw new InvalidOperationException($"Event channel {name} is stopping.");
                }
                if (_activeEventChannels.ContainsKey(name)) {
                    throw new InvalidOperationException($"Event sink {name} already exists.");
                }

                newChannel = createChannel(sink);
                if (_running)
                    TryStartChannel(name, newChannel, _stoppingTokenSource.Token);

                _failedEventChannels = _failedEventChannels.Remove(name);
                _activeEventChannels = _activeEventChannels.Add(name, newChannel);
            }
            return newChannel;
        }

        (IImmutableDictionary<string, EventChannel> active, IImmutableDictionary<string, EventChannel> closed, IImmutableDictionary<string, EventChannel> failed) ClearEventChannels() {
            lock (_eventChannelLock) {
                _stopping = true;
                var active = this._activeEventChannels;
                var closed = this._closedEventChannels;
                var failed = this._failedEventChannels;
                this._activeEventChannels = ImmutableDictionary<string, EventChannel>.Empty.WithComparers(StringComparer.CurrentCultureIgnoreCase);
                this._closedEventChannels = ImmutableDictionary<string, EventChannel>.Empty.WithComparers(StringComparer.CurrentCultureIgnoreCase);
                this._failedEventChannels = ImmutableDictionary<string, EventChannel>.Empty.WithComparers(StringComparer.CurrentCultureIgnoreCase);
                return (active, closed, failed);
            }
        }

        public void PostEvent(TraceEvent evt) {
            var eventChannels = this._activeEventChannels;
            foreach (var entry in eventChannels) {
                var success = entry.Value.PostEvent(evt);
                if (!success) {
                    //TODO move the entry to closed or failed channels?
                }
            }
        }

        public async Task Process(RealTimeTraceSession session, CancellationToken stoppingToken) {
            // we link _stoppingTokenSource to this stoppingToken so that it cancels all event channels as well
            var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var oldCts = Interlocked.Exchange(ref _stoppingTokenSource, cts);
            if (oldCts != null) {
                oldCts.Cancel();
                oldCts.Dispose();
            }

            // start initial channels
            ImmutableDictionary<string, EventChannel> initialChannels;
            lock (_eventChannelLock) {
                _running = true;
                initialChannels = _activeEventChannels;
            }
            foreach (var entry in initialChannels) {
                TryStartChannel(entry.Key, entry.Value, cts.Token);
            }

            // when cts.Token gets cancelled then the session gets disposed
            await session.StartEvents(PostEvent, cts.Token).ConfigureAwait(false);

            // no more events will be posted, wait for channels to complete
            ImmutableDictionary<string, EventChannel> activeChannels;
            // do not allow more channels to be added
            lock (_eventChannelLock) {
                _stopping = true;
                activeChannels = _activeEventChannels;
            }
            var activeRunTasks = activeChannels.Select(ac => ac.Value.RunTask!).Where(rt => rt != null).ToList();
            await Task.WhenAll(activeRunTasks).ConfigureAwait(false);
        }

        // should only be called after Process() task has finished
        public async ValueTask DisposeAsync() {
            // clear channels and prevent more channels from being added
            var (activeChannels, closedChannels, failedChannels) = ClearEventChannels();
            // stop processing
            var cts = _stoppingTokenSource;
            if (cts != null && !cts.IsCancellationRequested) {
                cts.Cancel();
            }

            try {
                var activeRunTasks = activeChannels.Select(ac => ac.Value.RunTask!).Where(rt => rt != null).ToList();
                var waitAllTask = Task.WhenAll(activeRunTasks).ContinueWith(art => art.Exception); // observe Exception
                var timeoutTask = Task.Delay(5000); // we don't want to wait forever
                await Task.WhenAny(waitAllTask, timeoutTask).ConfigureAwait(false);
            }
            catch {
                // just to make sure, should not throw anyway
            }
            finally {
                var taskList = new List<ValueTask>(activeChannels.Select(ac => ac.Value.DisposeAsync()));
                taskList.AddRange(closedChannels.Select(ac => ac.Value.DisposeAsync()));
                taskList.AddRange(failedChannels.Select(ac => ac.Value.DisposeAsync()));
                foreach (var vt in taskList) {
                    await vt.ConfigureAwait(false);
                }

                if (cts != null) {
                    cts.Dispose();
                }
            }
        }
    }
}
