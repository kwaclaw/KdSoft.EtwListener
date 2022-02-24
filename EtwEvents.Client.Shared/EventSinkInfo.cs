using System;

namespace KdSoft.EtwEvents.Client
{
    public class EventSinkInfo
    {
        public string SinkType { get; set; } = nameof(NullSink);
        public string Version { get; set; } = "0.0";
        public string CredentialsSchema { get; set; } = "";
        public string OptionsSchema { get; set; } = "";
    }
}
