namespace KdSoft.EtwEvents.EventSinks
{
    public class ElasticSinkOptions
    {
        public ElasticSinkOptions(string[] nodes, string indexFormat) : this() {
            this.Nodes = nodes;
            this.IndexFormat = indexFormat;
        }

        public ElasticSinkOptions() { }

        public string[] Nodes { get; set; } = Array.Empty<string>();

        public string? CloudId { get; set; }

        public string IndexFormat { get; set; } = string.Empty;
    }
}
