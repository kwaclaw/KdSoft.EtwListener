using System;
using System.Threading.Tasks;
using KdSoft.EtwLogging;
using Microsoft.Extensions.Logging;

namespace KdSoft.EtwEvents.PushAgent
{
    /// <summary>
    /// Proxy for <see cref="IEventSink"/> that handles sink failures on WriteAsync()
    /// by closing/disposing of the event sink, and re-creating it on the next call to WriteAsync().
    /// </summary>
    static class EventSinkExtensions {
        static async Task<IEventSinkFactory?> LoadSinkFactory(EventSinkService sinkService, string sinkType, string version, ILogger logger) {
            var sinkFactory = sinkService.LoadEventSinkFactory(sinkType, version);
            if (sinkFactory == null) {
                logger.LogInformation("Downloading event sink factory '{sinkType}~{version}'.", sinkType, version);
                await sinkService.DownloadEventSink(sinkType, version);
            }
            sinkFactory = sinkService.LoadEventSinkFactory(sinkType, version);
            return sinkFactory;
        }

        public static async Task<EventSinkRetryProxy> Create(EventSinkProfile profile, EventSinkService sinkService, ILoggerFactory loggerFactory) {
            var factoryLogger = loggerFactory.CreateLogger<IEventSinkFactory>();
            var sinkFactory = await LoadSinkFactory(sinkService, profile.SinkType, profile.Version, factoryLogger).ConfigureAwait(false);
            if (sinkFactory == null)
                throw new EventSinkException("Failed to create event sink factory.") {
                    Data = { { "SinkType", profile.SinkType }, { "Version", profile.Version } }
                };
            var sinkId = $"{profile.SinkType}~{profile.Version}::{profile.Name}";
            var retryStrategy = new BackoffRetryStrategy(
                TimeSpan.FromMilliseconds(200),
                TimeSpan.FromSeconds(15),
                TimeSpan.FromHours(2),
                forever: true);
            return new EventSinkRetryProxy(sinkId, profile.Options, profile.Credentials, sinkFactory, loggerFactory, retryStrategy);
        }
    }
}
