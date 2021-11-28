using System.Collections.Generic;
using KdSoft.EtwLogging;

namespace KdSoft.EtwEvents.AgentManager.Services
{
    public class FilterHelper
    {
        public static Filter MergeFilterTemplate(IReadOnlyList<FilterPart> dynamicParts) {
            var filter = new Filter();
            // merge template parts with application provided (dynamic) filterparts
            int lastEmptyIndx = Constants.FilterTemplateParts.Length - 2;
            for (int indx = 0; indx < Constants.FilterTemplateParts.Length; indx++) {
                var templatePart = Constants.FilterTemplateParts[indx];
                filter.FilterParts.Add(new FilterPart { Name = "template", Lines = { templatePart } });
                if (indx < dynamicParts.Count) {
                    var dynamicPart = dynamicParts[indx];
                    if (dynamicPart.Lines.Count == 0) {
                        dynamicPart.Lines.Add("");
                    }
                    filter.FilterParts.Add(dynamicPart);
                }
                else if (indx <= lastEmptyIndx) {
                    filter.FilterParts.Add(new FilterPart { Name = "empty", Lines = { "" } });
                }
            }
            return filter;
        }

    }
}
