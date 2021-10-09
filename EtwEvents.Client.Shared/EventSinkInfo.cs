using System;

namespace KdSoft.EtwEvents.Client.Shared
{
    public class EventSinkInfo
    {
        public string SinkType { get; set; } = nameof(DummySink);
        public Uri? ConfigViewUrl { get; set; }
        public Uri? ConfigModelUrl { get; set; }
    }
}
