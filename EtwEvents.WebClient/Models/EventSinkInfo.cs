using KdSoft.EtwEvents.WebClient.EventSinks;

namespace KdSoft.EtwEvents.WebClient.Models
{
    public class EventSinkInfo
    {
        public string SinkType { get; set; } = nameof(DummySink);
        public string Description { get; set; } = "Dummy";
    }
}
