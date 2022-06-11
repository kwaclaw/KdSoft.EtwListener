using System;

namespace KdSoft.EtwEvents.EventSinks
{
    public class OpenSearchSinkOptions
    {
        public OpenSearchSinkOptions() { }

        public OpenSearchSinkOptions(string[] nodes, string indexFormat) : this() {
            this.Nodes = nodes;
            this.IndexFormat = indexFormat;
        }

        public string[] Nodes { get; set; } = Array.Empty<string>();

        public string IndexFormat { get; set; } = string.Empty;
    }
}
