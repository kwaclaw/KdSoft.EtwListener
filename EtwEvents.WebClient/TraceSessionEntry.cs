using System;
using System.Threading;
using System.Threading.Tasks;
using KdSoft.Utils;

namespace EtwEvents.WebClient
{
    class TraceSessionEntry: ILifeCycleAware<ITimedLifeCycle>
    {
        readonly TimedLifeCycle _lifeCycle;

        public TraceSessionEntry(Task<TraceSession> createTask, TimeSpan lifeTime) {
            this.CreateTask = createTask;
            _lifeCycle = new TimedLifeCycle(lifeTime);
            _lifeCycle.Ended += _lifeCycle_Ended;
        }

        void _lifeCycle_Ended(object? sender, EventArgs e) {
            _disposeTask = CreateTask.ContinueWith(async t => {
                try {
                    await t.Result.DisposeAsync();
                }
                catch (Exception ex) {
                    //_logger.LogError(new EventId(0, "session-dispose"), ex, "");
                }
            }, CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Current);
        }

        public ITimedLifeCycle GetLifeCycle() {
            return _lifeCycle;
        }

        Task? _disposeTask;
        public Task<TraceSession> CreateTask { get; }
        public Task? DisposeTask => _disposeTask;

        public TraceSession Instance => CreateTask.Result;
    }
}
