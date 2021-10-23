using System.Collections.Immutable;
using KdSoft.EtwEvents.Client;
using KdSoft.EtwLogging;

namespace KdSoft.EtwEvents.PushAgent.Models
{
    public class AgentState
    {
        public string Id { get; set; } = string.Empty;
        public string Site { get; set; } = string.Empty;
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
        public EventSinkState EventSink { get; set; } = new EventSinkState();
        public string? FilterBody { get; set; }
    }

    public class EventSinkState
    {
        public EventSinkProfile? Profile { get; set; }
        public string? Error { get; set; }
    }

    public class AgentStates
    {
        public ImmutableArray<AgentState> Agents { get; set; }
    }
}
