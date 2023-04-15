namespace KdSoft.EtwEvents.EventSinks
{
    public class DataCollectorSinkCredentials
    {
        public DataCollectorSinkCredentials() { }

        public DataCollectorSinkCredentials(string sharedKey) : this() {
            this.SharedKey = sharedKey;
        }

        public string SharedKey { get; set; } = string.Empty;
    }
}
