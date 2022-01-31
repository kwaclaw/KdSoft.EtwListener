using System;
using System.Threading.Tasks;
using KdSoft.EtwLogging;
using Microsoft.Extensions.Logging;

namespace KdSoft.EtwEvents.PushAgent
{
    static class EventSinkExtensions {
        static async Task<(IEventSinkFactory?, EventSinkLoadContext? loadContext)> LoadSinkFactory(EventSinkService sinkService, string sinkType, string version, ILogger logger) {
            var (sinkFactory, loadContext) = sinkService.LoadEventSinkFactory(sinkType, version);
            if (sinkFactory == null) {
                logger.LogInformation("Downloading event sink factory '{sinkType}~{version}'.", sinkType, version);
                await sinkService.DownloadEventSink(sinkType, version);
            }
            (sinkFactory, loadContext) = sinkService.LoadEventSinkFactory(sinkType, version);
            return (sinkFactory, loadContext);
        }

        public static async Task<EventSinkRetryProxy> CreateRetryProxy(this EventSinkProfile profile, EventSinkService sinkService, IRetryStrategy retryStrategy, ILoggerFactory loggerFactory) {
            var factoryLogger = loggerFactory.CreateLogger<IEventSinkFactory>();
            var (sinkFactory, loadContext) = await LoadSinkFactory(sinkService, profile.SinkType, profile.Version, factoryLogger).ConfigureAwait(false);
            if (sinkFactory == null)
                throw new EventSinkException("Failed to create event sink factory.") {
                    Data = { { "SinkType", profile.SinkType }, { "Version", profile.Version } }
                };
            var sinkId = $"{profile.SinkType}~{profile.Version}::{profile.Name}";
            return new EventSinkRetryProxy(sinkId, profile.Options, profile.Credentials, sinkFactory, loadContext, loggerFactory, retryStrategy);
        }
    }
}
