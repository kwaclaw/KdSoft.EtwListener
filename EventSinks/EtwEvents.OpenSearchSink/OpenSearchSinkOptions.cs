using System;

namespace KdSoft.EtwEvents.EventSinks
{
    public class OpenSearchSinkOptions
    {
        public OpenSearchSinkOptions(string[] nodes, string indexFormat) : this() {
            this.Nodes = nodes;
            this.IndexFormat = indexFormat;
        }

        public OpenSearchSinkOptions() { }

        public string[] Nodes { get; set; } = Array.Empty<string>();

        public string IndexFormat { get; set; } = string.Empty;
    }
}
