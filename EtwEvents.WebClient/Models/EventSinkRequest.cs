using KdSoft.EtwEvents.Client.Shared;
using KdSoft.EtwEvents.WebClient.EventSinks;

namespace KdSoft.EtwEvents.WebClient.Models
{
    public class EventSinkRequest
    {
        public string SinkType { get; set; } = nameof(DummySink);
        public string Name { get; set; } = "Dummy";
        public object Options { get; set; } = new { };
        public object Credentials { get; set; } = new { };
    }
}
