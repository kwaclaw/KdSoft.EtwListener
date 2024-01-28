namespace KdSoft.EtwEvents.EventSinks
{
    public class LogsIngestionSinkOptions
    {
        public LogsIngestionSinkOptions() { }

        public LogsIngestionSinkOptions(string endPoint, string ruleId, string streamName) : this() {
            this.EndPoint = endPoint;
            this.RuleId = ruleId;
            this.StreamName = streamName;
        }

        public string EndPoint { get; set; } = string.Empty;

        public string RuleId { get; set; } = string.Empty;

        public string StreamName { get; set; } = string.Empty;
    }
}
