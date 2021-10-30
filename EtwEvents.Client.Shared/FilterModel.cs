using System;

namespace KdSoft.EtwEvents
{
    public class FilterModel
    {
        public string FilterTemplate { get; set; } = string.Empty;

        // the last element is the method body for IncludeEvent()
        public string[] FilterParts { get; set; } = Array.Empty<string>();
    }
}
