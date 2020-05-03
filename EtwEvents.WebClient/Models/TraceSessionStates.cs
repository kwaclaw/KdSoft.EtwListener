using System.Collections.Immutable;
using KdSoft.EtwLogging;

namespace KdSoft.EtwEvents.WebClient.Models
{
    public class TraceSessionState
    {
        public string Name { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public bool IsRunning { get; set; }
        public IImmutableList<ProviderSetting> EnabledProviders { get; set; } = ImmutableList<ProviderSetting>.Empty;
        public ImmutableArray<EventSinkState> EventSinks { get; set; }
    }

    public class OpenSessionState: TraceSessionState
    {
        public IImmutableList<string> RestartedProviders { get; set; } = ImmutableList<string>.Empty;
        public bool AlreadyOpen { get; set; }
    }

    public class EventSinkState
    {
        public string SinkType { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Error { get; set; }
    }

    public class TraceSessionStates
    {
        public ImmutableArray<TraceSessionState> Sessions { get; set; }
    }
}
