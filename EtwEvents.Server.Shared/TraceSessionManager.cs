using KdSoft.Utils;

namespace KdSoft.EtwEvents.Server
{
    public class TraceSessionManager: ConcurrentTimedLifeCycleManager<string, RealTimeTraceSession>
    {
        public TraceSessionManager(TimeSpan reapPeriod) : base(reapPeriod) {
            //
        }
    }
}
