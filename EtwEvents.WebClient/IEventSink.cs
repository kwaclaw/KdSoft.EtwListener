using System;
using System.Threading;
using System.Threading.Tasks;
using KdSoft.EtwLogging;

namespace EtwEvents.WebClient
{
    public interface IEventSink: IAsyncDisposable, IDisposable
    {
        void Initialize(CancellationToken stoppingToken);

        Task<bool> WriteAsync(EtwEvent evt, long sequenceNo, CancellationToken stoppingToken);
    }
}
