namespace KdSoft.EtwEvents
{
    public static class Constants
    {
        public const string EventStreamHeaderValue = "text/event-stream";
        public const string CloseEvent = "##close";
        public const string KeepAliveEvent = "##keepAlive";
        public const string StartEvent = "Start";
        public const string StopEvent = "Stop";
        public const string GetStateEvent = "GetState";
        public const string SetEmptyFilterEvent = "SetEmptyFilter";
        public const string UpdateProvidersEvent = "UpdateProfiders";
        public const string ApplyProcessingOptionsEvent = "ApplyProcessingOptions";
        public const string TestFilterEvent = "TestFilter";
        public const string UpdateEventSinksEvent = "UpdateEventSinks";
        public const string CloseEventSinkEvent = "CloseEventSink";
        public const string StartManagerSinkEvent = "StartManagerSink";

        public const string X500DistNameClaim = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/x500distinguishedname";
    }
}
