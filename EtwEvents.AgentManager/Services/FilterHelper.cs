using System.Collections.Generic;
using System.Collections.Immutable;
using KdSoft.EtwLogging;

namespace KdSoft.EtwEvents.AgentManager
{
    public class FilterHelper
    {
        public static Filter MergeFilterTemplate(IReadOnlyList<string>? dynamicParts = null) {
            if (dynamicParts == null)
                dynamicParts = ImmutableArray<string>.Empty;

            var filterParts = ImmutableArray<FilterPart>.Empty;
            int dynamicIndx = 0;
            for (int indx = 0; indx < Constants.FilterTemplateParts.Length; indx++) {
                var clonedPart = Constants.FilterTemplateParts[indx].Clone();
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

            var filter = new Filter {
                TemplateVersion = Constants.FilterTemplateVersion,
                FilterParts = { filterParts }
            };
            return filter;
        }
    }
}
