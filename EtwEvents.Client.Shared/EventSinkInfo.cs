using System;

namespace KdSoft.EtwEvents.Client.Shared
{
    public class EventSinkInfo
    {
        public string SinkType { get; set; } = nameof(DummySink);
        public string Description { get; set; } = "Dummy";
        public Uri? ConfigViewUrl { get; set; }
        public Uri? ConfigModelUrl { get; set; }
    }
}
