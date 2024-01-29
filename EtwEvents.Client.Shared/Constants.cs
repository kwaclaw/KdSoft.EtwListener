namespace KdSoft.EtwEvents
{
    public static class Constants
    {
        public const string TraceSessionNamePrefix = "KdSoft-ETW-Agent";
        public const string EventStreamHeaderValue = "text/event-stream";
        public const string CloseEvent = "##close";
        public const string KeepAliveEvent = "##keepAlive";
        public const string StartEvent = "Start";
        public const string StopEvent = "Stop";
        public const string GetStateEvent = "GetState";
        public const string SetEmptyFilterEvent = "SetEmptyFilter";
        public const string ApplyAgentOptionsEvent = "ApplyAgentOptions";
        public const string TestFilterEvent = "TestFilter";
        public const string CloseEventSinkEvent = "CloseEventSink";
        public const string LiveViewSinkName = "##LiveViewSink";
        public const string StartLiveViewSinkEvent = "StartLiveViewSink";
        public const string StopLiveViewSinkEvent = "StopLiveViewSink";
        public const string ResetEvent = "Reset";
        public const string InstallCertEvent = "InstallCert";
        public const string SetControlOptionsEvent = "SetControlOptions";

        public const string X500DistNameClaim = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/x500distinguishedname";
    }
}
