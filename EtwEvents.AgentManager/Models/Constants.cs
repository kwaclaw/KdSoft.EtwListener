using System.Collections.Immutable;

namespace KdSoft.EtwEvents.AgentManager
{
    public static class Constants
    {
        public const string EventStreamHeaderValue = "text/event-stream";
        public const string CloseEvent = "##close";
        public const string KeepAliveEvent = "##keepAlive";
        public const string GetStateEvent = "GetState";

        public const int FilterTemplateVersion = 1;

        public const string X500DistNameClaim = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/x500distinguishedname";

        public static readonly ImmutableArray<string> FilterLines1 = new string[] {
            "using System.Linq;",
            "using Microsoft.Diagnostics.Tracing;",
            "using Microsoft.Extensions.Configuration;",
        }.ToImmutableArray();

        public static readonly ImmutableArray<string> FilterLines2 = new string[] {
            "",
            "namespace KdSoft.EtwEvents.Server",
            "{",
            "    public class EventFilter: IEventFilter",
            "    {",
            "        readonly IConfiguration _config;",
            "",
        }.ToImmutableArray();

        public static readonly ImmutableArray<string> FilterLines3 = new string[] {
            "",
            "        public EventFilter(IConfiguration config) {",
            "            this._config = config;",
            "            Init();",
            "        }",
            "",
            "        void Init() {"
        }.ToImmutableArray();

        public static readonly ImmutableArray<string> FilterLines4 = new string[] {
            "        }",
            "",
            "        public bool IncludeEvent(TraceEvent evt) {",
        }.ToImmutableArray();

        public static readonly ImmutableArray<string> FilterLines5 = new string[] {
            "        }",
            "    }",
            "}",
        }.ToImmutableArray();

        public static readonly ImmutableArray<ImmutableArray<string>> FilterTemplateParts = ImmutableArray<ImmutableArray<string>>.Empty
            .Add(FilterLines1)
            .Add(FilterLines2)
            .Add(FilterLines3)
            .Add(FilterLines4)
            .Add(FilterLines5);
    }
}
