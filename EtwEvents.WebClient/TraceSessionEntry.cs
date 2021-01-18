using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using kdsoft = KdSoft.Utils;

namespace KdSoft.EtwEvents.WebClient
{
    class TraceSessionEntry: kdsoft.ILifeCycleAware<kdsoft.ITimedLifeCycle>
    {
        readonly kdsoft.TimedLifeCycle _lifeCycle;

        public TraceSessionEntry(Lazy<Task<TraceSession>> createTask, TimeSpan lifeTime) {
            this.SessionTask = createTask;
            _lifeCycle = new kdsoft.TimedLifeCycle(lifeTime);
            _lifeCycle.Ended += _lifeCycle_Ended;
        }

        void _lifeCycle_Ended(object? sender, EventArgs e) {
            if (this.SessionTask.IsValueCreated) {
                _disposeTask = SessionTask.Value.ContinueWith(async t => {
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
        }

        public kdsoft.ITimedLifeCycle GetLifeCycle() {
            return _lifeCycle;
        }

        Task? _disposeTask;
        public Lazy<Task<TraceSession>> SessionTask { get; }
        public Task? DisposeTask => _disposeTask;

        /// <summary>
        /// Allows us to use <c>var traceSession = await [instance of TraceSessionEntry]</c>
        /// </summary>
        /// <returns></returns>
        public TaskAwaiter<TraceSession> GetAwaiter() { return SessionTask.Value.GetAwaiter(); }

        /// <summary>
        /// Allows us to use <c>var traceSession = await [instance of TraceSessionEntry].ConfigureAwait()</c>
        /// </summary>
        /// <returns></returns>
        public ConfiguredTaskAwaitable<TraceSession> ConfigureAwait(bool value) { return SessionTask.Value.ConfigureAwait(value); }
    }
}
