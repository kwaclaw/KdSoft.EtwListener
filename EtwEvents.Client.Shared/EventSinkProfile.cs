using System.Collections.Generic;

namespace KdSoft.EtwEvents.Client.Shared
{
    public class EventSinkProfile
    {
        public string SinkType { get; set; } = nameof(DummySink);
        public string Name { get; set; } = "Dummy";
        public Dictionary<string,object> Options { get; set; } = new Dictionary<string, object>();
        public Dictionary<string, object> Credentials { get; set; } = new Dictionary<string, object>();
    }
}
