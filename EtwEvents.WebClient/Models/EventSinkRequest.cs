using EtwEvents.WebClient.EventSinks;

namespace EtwEvents.WebClient.Models
{
    public class EventSinkRequest
    {
        public string SinkType { get; set; } = nameof(DummySink);
        public string Name { get; set; } = "Dummy";
        public dynamic Options { get; set; } = new { };
    }
}
