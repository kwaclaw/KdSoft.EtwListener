namespace KdSoft.EtwEvents.EventSinks
{
    public class ElasticSinkOptions
    {
        public ElasticSinkOptions(string[] nodes, string index) {
            this.Nodes = nodes;
            this.Index = index;
        }

        public ElasticSinkOptions() { }

        public string[] Nodes { get; set; } = new string[0];

        public string Index { get; set; } = string.Empty;
    }
}
