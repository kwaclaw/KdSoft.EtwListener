using Google.Protobuf.WellKnownTypes;
using KdSoft.EtwEvents;
using KdSoft.EtwEvents.EventSinks;
using KdSoft.EtwLogging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EtwEvents.AzureDataCollectorSink.Tests
{
    public class UnitTests
    {
        readonly DataCollectorSinkFactory _sinkFactory;

        public UnitTests() {
            _sinkFactory = new DataCollectorSinkFactory();
        }

        record struct EventSinkContext(string SiteName, ILogger Logger): IEventSinkContext;

        [Fact]
        public async void IngestBatch() {
            var opts = new DataCollectorSinkOptions {
                CustomerId = "1ce7293d-adc8-4fab-b242-04a15bfb8f1b",
                LogType = "etw_analytics_demo"
            };
            var sharedKey = "i2lhM0ILc4o8Te+JhSdKNXkjMWWRU1txHE9wpXfjLkACzkoCK1O7nwKb2EJi3Ejv6l8kbVuXgaD/9o5rwJPSEA==";
            var eventSink = await _sinkFactory.Create(opts, sharedKey, new EventSinkContext("demo-site-1", NullLogger.Instance));
            try {
                var batch = new EtwEventBatch();
                for (int i = 1; i <= 80; i++) {
                    var evt = new EtwEvent() { Id = (uint)i, TimeStamp = DateTimeOffset.UtcNow.ToTimestamp() };
                    batch.Events.Add(evt);
                }
                await eventSink.WriteAsync(batch);
            }
            finally {
                await eventSink.DisposeAsync();
            }

            await eventSink.RunTask;
        }
    }
}