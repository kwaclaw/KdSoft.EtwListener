namespace KdSoft.EtwEvents.EventSinks
{
    public class SeqSinkCredentials
    {
        public SeqSinkCredentials() { }

        public SeqSinkCredentials(string apiKey) : this() {
            this.ApiKey = apiKey;
        }

        public string ApiKey { get; set; } = string.Empty;
    }
}
