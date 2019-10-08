using System;
using KdSoft.Utils;

namespace EtwEvents.Server
{
    class TraceSessionManager: ConcurrentTimedLifeCycleManager<string, TraceSession>
    {
        public TraceSessionManager(TimeSpan reapPeriod) : base(reapPeriod) {
            //
        }
    }
}
