using System.Collections.Immutable;
using KdSoft.EtwLogging;

namespace KdSoft.EtwEvents.AgentManager.Models
{
    public class AgentState
    {
        public string Name { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;

        /// <summary>
        /// Indicates if the session has started and has not ended yet.
        /// </summary>
        public bool IsRunning { get; set; }

        /// <summary>
        /// Indicates if session has started and then stopped.
        /// Would indicate <c>false</c> if session has not started yet.
        /// </summary>
        public bool IsStopped { get; set; }

        public IImmutableList<ProviderSetting> EnabledProviders { get; set; } = ImmutableList<ProviderSetting>.Empty;
        public ImmutableArray<EventSinkState> EventSinks { get; set; }
    }

    public class EventSinkState
    {
        public string SinkType { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Error { get; set; }
    }

    public class AgentStates
    {
        public ImmutableArray<AgentState> Agents { get; set; }
    }
}
