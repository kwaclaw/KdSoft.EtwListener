using KdSoft.EtwEvents.EventSinks;

namespace KdSoft.EtwEvents.PushClient {
    public class EventSinkOptions {
        public string SinkType { get; set; } = "";
        public string Name { get; set; } = "";
        public EventSinkDefinition Definition { get; set; } = new EventSinkDefinition();
    }

    public class EventSinkDefinition {
        public ElasticSinkOptions Options { get; set; } = new ElasticSinkOptions();

        public ElasticSinkCredentials Credentials { get; set; } = new ElasticSinkCredentials();
    }
}
