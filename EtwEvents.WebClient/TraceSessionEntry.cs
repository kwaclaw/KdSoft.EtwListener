using System;
using System.Threading;
using System.Threading.Tasks;
using KdSoft.Utils;

namespace KdSoft.EtwEvents.WebClient
{
    class TraceSessionEntry: ILifeCycleAware<ITimedLifeCycle>
    {
        readonly TimedLifeCycle _lifeCycle;

        public TraceSessionEntry(Task<TraceSession> createTask, TimeSpan lifeTime) {
            this.SessionTask = createTask;
            _lifeCycle = new TimedLifeCycle(lifeTime);
            _lifeCycle.Ended += _lifeCycle_Ended;
        }

        void _lifeCycle_Ended(object? sender, EventArgs e) {
            _disposeTask = SessionTask.ContinueWith(async t => {
                var ts = t.Result;  // will not throw, see TaskContinuationOptions.OnlyOnRanToCompletion
                try {
                    await ts.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception ex) {
                    //_logger.LogError(new EventId(0, "session-dispose"), ex, "");
                }
                finally {
                    await ts.PostSessionStateChange().ConfigureAwait(false);
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
