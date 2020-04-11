using System;
using System.Threading;
using System.Threading.Tasks;
using KdSoft.Utils;

namespace EtwEvents.WebClient
{
    class TraceSessionEntry: ILifeCycleAware<ITimedLifeCycle>
    {
        readonly TimedLifeCycle _lifeCycle;
        readonly Func<ValueTask> _onDisposed;

        public TraceSessionEntry(Task<TraceSession> createTask, TimeSpan lifeTime, Func<ValueTask> onDisposed) {
            this.SessionTask = createTask;
            _lifeCycle = new TimedLifeCycle(lifeTime);
            _lifeCycle.Ended += _lifeCycle_Ended;
            _onDisposed = onDisposed;
        }

        void _lifeCycle_Ended(object? sender, EventArgs e) {
            _disposeTask = SessionTask.ContinueWith(async t => {
                try {
                    await t.Result.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception ex) {
                    //_logger.LogError(new EventId(0, "session-dispose"), ex, "");
                }
                finally {
                    await _onDisposed().ConfigureAwait(false);
                }
            }, CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Current);
        }

        public ITimedLifeCycle GetLifeCycle() {
            return _lifeCycle;
        }

        Task? _disposeTask;
        public Task<TraceSession> SessionTask { get; }
        public Task? DisposeTask => _disposeTask;
    }
}
