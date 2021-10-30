namespace KdSoft.EtwEvents.AgentManager
{
    public static class Constants
    {
        public const string EventStreamHeaderValue = "text/event-stream";
        public const string CloseEvent = "##close";
        public const string KeepAliveEvent = "##keepAlive";
        public const string GetStateEvent = "GetState";

        public const string X500DistNameClaim = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/x500distinguishedname";

        public const string FilterTemplate = @"using System;
using System.Linq;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Extensions.Configuration;
{0}

namespace KdSoft.EtwEvents.Server
{{
    public class EventFilter: IEventFilter
    {{
        readonly IConfiguration _config;

        {1}

        public EventFilter(IConfiguration config) {{
            this._config = config;
            Init();
        }}

        void Init() {{
            {2}
        }}

        public bool IncludeEvent(TraceEvent evt) {{
            {3}
        }}
    }}
}}
";
    }
}
