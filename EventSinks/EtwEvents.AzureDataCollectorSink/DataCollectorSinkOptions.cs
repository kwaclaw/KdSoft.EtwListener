namespace KdSoft.EtwEvents.EventSinks
{
    public class DataCollectorSinkOptions
    {
        public DataCollectorSinkOptions() { }

        public DataCollectorSinkOptions(string customerId, string logType) : this() {
            this.CustomerId = customerId;
            this.LogType = logType;
        }

        public string CustomerId { get; set; } = string.Empty;

        public string LogType { get; set; } = string.Empty;

        public string? ResourceId { get; set; }
    }
}
