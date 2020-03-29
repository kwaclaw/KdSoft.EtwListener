using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using KdSoft.EtwLogging;

namespace EtwEvents.WebClient.Models
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

    internal static class TraceSessionStateExtensions
    {
        public static async Task<TraceSessionState> GetSessionState(this TraceSession session) {
            if (session is null)
                throw new ArgumentNullException(nameof(session));
            await session.UpdateSessionState().ConfigureAwait(false);
            return GetSessionStateSnapshot<TraceSessionState>(session);
        }

        public static T GetSessionStateSnapshot<T>(this TraceSession session) where T: TraceSessionState, new() {
            if (session is null)
                throw new ArgumentNullException(nameof(session));
            var result = new T { 
                Name = session.Name ?? string.Empty,
                Host = session.Host,
                IsRunning = !session.EventStream.IsCompleted,
                EnabledProviders = session.EnabledProviders
            };
            var activeEventSinks = session.EventSinks.ActiveEventSinks.Select(
                aes => new EventSinkState { SinkType = aes.Value.GetType().Name, Name = aes.Key }
            ).ToImmutableArray();
            var eventSinks = activeEventSinks.AddRange(session.EventSinks.FailedEventSinks.Select(
                fes => new EventSinkState { SinkType = fes.Value.GetType().Name, Name = fes.Key, Error = fes.Value.error?.Message }
            ));
            result.EventSinks = eventSinks;
            return result;
        }
    }
}
