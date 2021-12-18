using System.Threading;
using System.Threading.Tasks;

namespace KdSoft.EtwEvents.Server
{
    public class EventProcessor
    {
        readonly EventChannelManager _channelHolder;

        public EventProcessor(EventChannelManager channelHolder) {
            this._channelHolder = channelHolder;
        }

        public async Task Process(RealTimeTraceSession session, CancellationToken stoppingToken) {
            _channelHolder.StartProcessing(stoppingToken);
            await session.StartEvents(_channelHolder.PostEvent, stoppingToken).ConfigureAwait(false);
            // no more events will be posted, wait for channels to complete
            await _channelHolder.StopProcessing().ConfigureAwait(false);
        }
    }
}
