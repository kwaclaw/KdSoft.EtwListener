namespace KdSoft.EtwLogging
{
    partial class EventSinkStatus
    {
        partial void OnConstruction() {
            //
        }

        public long? RetryStartTimeMSecs => (RetryStartTime == null) ? null : (RetryStartTime.Seconds * 1000) + (RetryStartTime.Nanos / 1000000);
    }
}
