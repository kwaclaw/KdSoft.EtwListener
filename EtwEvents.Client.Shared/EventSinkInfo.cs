using System;

namespace KdSoft.EtwEvents.Client
{
    public class EventSinkInfo
    {
        public string SinkType { get; set; } = nameof(DummySink);
        public string Version { get; set; } = "0.0";
        public Uri? ConfigViewUrl { get; set; }
        public Uri? ConfigModelUrl { get; set; }
    }
}
