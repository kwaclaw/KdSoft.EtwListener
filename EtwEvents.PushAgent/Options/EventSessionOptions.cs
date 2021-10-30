using System.Collections.Generic;

namespace KdSoft.EtwEvents.PushAgent
{
    public class EventSessionOptions: ProcessingOptions
    {
        public List<ProviderOptions> Providers { get; set; } = new List<ProviderOptions>();
    }
}
