using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing;

namespace KdSoft.EtwEvents.Server
{
    class RealTimeTraceEventSource: IDisposable
    {
        public readonly ETWTraceEventSource Source;
        public readonly Task ProcessTask;

        public RealTimeTraceEventSource(
            TraceSession session,
            Func<TraceEvent, Task> postEvent,
            TaskCompletionSource<object?> tcs,
            CancellationToken cancelToken
        ) {
            var filter = session.GetFilter();

            Source = new ETWTraceEventSource(session.SessionName, TraceEventSourceType.Session);

            async void handleEvent(TraceEvent evt) {
                if (cancelToken.IsCancellationRequested) {
                    Source.StopProcessing(); // cannot continue once we have stopped
                    tcs.TrySetResult(null);
                    return;
                }
                if (TplActivities.TplEventSourceGuid.Equals(evt.ProviderGuid))
                    return;

                session.CheckFilterChanged(ref filter);
                if (filter == null || filter.IncludeEvent(evt)) {
                    await postEvent(evt).ConfigureAwait(false);
                }
            }

            Source.Dynamic.All += handleEvent;

            void handleCompleted() => tcs.TrySetResult(null);
            Source.Completed += handleCompleted;

            // this cannot be called multiple times in real time mode
            ProcessTask = Task.Run(Source.Process).ContinueWith(t => {
                Debug.WriteLine(t.Exception?.ToString() ?? $"Error in {session.SessionName}");
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        public void Stop() {
            Source.StopProcessing();
        }

        public void Dispose() {
            Source.Dispose();
        }
    }
}
