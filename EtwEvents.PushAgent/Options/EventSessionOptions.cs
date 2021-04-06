using System.Collections.Generic;

namespace KdSoft.EtwEvents.PushAgent
{
    public class EventSessionOptions
    {
        public int BatchSize { get; set; } = 100;
        public int MaxWriteDelayMSecs { get; set; } = 400;
        public List<ProviderOptions> Providers { get; set; } = new List<ProviderOptions>();
        public string Filter { get; set; } = string.Empty;
    }
}
