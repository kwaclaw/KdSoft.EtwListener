namespace KdSoft.EtwEvents.EventSinks
{
    public class SeqSinkOptions
    {
        public SeqSinkOptions(string serverUrl, string proxyAddress) : this() {
            this.ServerUrl = serverUrl;
            this.ProxyAddress = proxyAddress;
        }

        public SeqSinkOptions() { }

        public string ServerUrl { get; set; } = string.Empty;


        public string ProxyAddress { get; set; } = string.Empty;
    }
}
