using System;
using KdSoft.Utils;

namespace KdSoft.EtwEvents.Server
{
    class TraceSessionManager: ConcurrentTimedLifeCycleManager<string, TraceSession>
    {
        public TraceSessionManager(TimeSpan reapPeriod) : base(reapPeriod) {
            //
        }
    }
}
