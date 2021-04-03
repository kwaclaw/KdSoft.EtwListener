using Microsoft.Diagnostics.Tracing;

namespace KdSoft.EtwEvents.PushAgent
{
    public class ProviderOptions
    {
        public string Name { get; set; } = "";
        public TraceEventLevel Level { get; set; }
        public ulong MatchKeywords { get; set; }
    }
}
