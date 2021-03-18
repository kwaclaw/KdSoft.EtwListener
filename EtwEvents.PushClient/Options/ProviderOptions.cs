using Microsoft.Diagnostics.Tracing;

namespace KdSoft.EtwEvents.PushClient
{
    public class ProviderOptions
    {
        public string Name { get; set; } = "";
        public TraceEventLevel Level { get; set; }
        public ulong MatchKeyWords { get; set; }
    }
}
