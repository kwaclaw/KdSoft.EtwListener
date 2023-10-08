using System.Collections.Immutable;
using KdSoft.EtwLogging;

namespace KdSoft.EtwEvents.AgentManager
{
    public class Filter
    {
        public const string FilterTemplate =
@"using System.Linq;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;
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

        public const int FilterTemplateVersion = 1;

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

        public static EtwLogging.Filter MergeFilterTemplate(IReadOnlyList<string>? dynamicParts = null) {
            dynamicParts ??= ImmutableArray<string>.Empty;

            var filterParts = ImmutableArray<FilterPart>.Empty;
            int dynamicIndx = 0;
            for (int indx = 0; indx < Filter.FilterTemplateParts.Length; indx++) {
                var clonedPart = Filter.FilterTemplateParts[indx].Clone();
                if (clonedPart.Name.StartsWith("dynamic")) {
                    if (dynamicIndx < dynamicParts.Count) {
                        var dynamicPart = dynamicParts[dynamicIndx++];
                        if (dynamicPart != null) {
                            clonedPart.Code = dynamicPart;
                        }
                    }
                }
                filterParts = filterParts.Add(clonedPart);
            }

            var filter = new EtwLogging.Filter {
                TemplateVersion = FilterTemplateVersion,
                FilterParts = { filterParts }
            };
            return filter;
        }
    }
}
