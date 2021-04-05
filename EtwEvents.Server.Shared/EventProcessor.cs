using System;
using System.Threading;
using System.Threading.Tasks;
using KdSoft.EtwLogging;

namespace KdSoft.EtwEvents.Server
{
    public delegate ValueTask<bool> WriteBatchAsync(EtwEventBatch batch);

    public abstract class EventProcessor {
        public abstract Task Process(RealTimeTraceSession session, TimeSpan maxWriteDelay, CancellationToken stoppingToken);
    }
}
