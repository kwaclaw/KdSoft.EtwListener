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
        readonly LogsIngestionSinkFactory _sinkFactory;

        public UnitTests() {
            _sinkFactory = new LogsIngestionSinkFactory();
        }

        record struct EventSinkContext(string SiteName, ILogger Logger): IEventSinkContext;

        [Fact]
        public async void IngestBatch() {
            // we rely on AzureCliCredential being logged in
            var opts = new LogsIngestionSinkOptions {
                EndPoint = "https://smartclinic-endpoint-gmkv.eastus-1.ingest.monitor.azure.com",
                RuleId = "dcr-46daf1d4228b4a8dbc5732e1824b5cb8",
                StreamName = "Custom-smartclinic_performance_CL"
            };
            var creds = new LogsIngestionSinkCredentials();
            var eventSink = await _sinkFactory.Create(opts, creds, new EventSinkContext("demo-site-2", NullLogger.Instance));
            try {
                var batch = new EtwEventBatch();
                for (int i = 1; i <= 80; i++) {
                    var evt = new EtwEvent {
                        Id = (uint)i,
                        TimeStamp = DateTimeOffset.UtcNow.ToTimestamp(),
                        Level = TraceEventLevel.Warning,
                        ProviderName = "CLI Test",
                        Payload = { { "Field1", "Value1" }, { "Field2", "Value2" }, { "Field3", $"{i}" } }
                    };
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