using System;
using KdSoft.Utils;

namespace EtwEvents.Server
{
    public class TraceSessionManager: ConcurrentTimedLifeCycleManager<string, TraceSession>
    {
        public TraceSessionManager(TimeSpan reapPeriod) : base(reapPeriod) {
            //
        }
    }
}
