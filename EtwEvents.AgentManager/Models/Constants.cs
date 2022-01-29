using System.Collections.Immutable;
using KdSoft.EtwLogging;

namespace KdSoft.EtwEvents.AgentManager
{
    public static class Constants
    {
        public const string EventStreamHeaderValue = "text/event-stream";
        public const string CloseEvent = "##close";
        public const string KeepAliveEvent = "##keepAlive";
        public const string GetStateEvent = "GetState";
        public const string SetEmptyFilterEvent = "SetEmptyFilter";
        public const string StartManagerSinkEvent = "StartManagerSink";

        public const int FilterTemplateVersion = 1;

        public const string X500DistNameClaim = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/x500distinguishedname";

        public const string FilterTemplate =
@"using System.Linq;
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
}}";

        static ImmutableArray<FilterPart> SplitTemplate() {
            var markedTemplate = string.Format(FilterTemplate, "\u001D0\u001D", "\u001D8\u001D", "\u001D12\u001D", "\u001D12\u001D");
            var parts = markedTemplate.Split(new char[] { '\u001D' });

            var result = ImmutableArray<FilterPart>.Empty;
            int partIndx = 0;
            foreach (var part in parts) {
                // dynamic parts have a non-zero indent as marked in the formatted Template
                if (part.Length <= 2 && int.TryParse(part, out var indent)) {  // we do not start with a dynamic part, we only have them in-between
                    var dynamicPart = new FilterPart { Name = $"dynamic{partIndx++}", Indent = indent, Code = "" };
                    result = result.Add(dynamicPart);
                }
                else {
                    var templatePart = new FilterPart { Name = $"template{partIndx++}", Indent = 0, Code = part };
                    result = result.Add(templatePart);
                }
            }
            return result;
        }

        public static readonly ImmutableArray<FilterPart> FilterTemplateParts = SplitTemplate();
    }
}
