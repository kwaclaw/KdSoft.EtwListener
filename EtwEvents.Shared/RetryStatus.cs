namespace KdSoft.EtwEvents
{
    public record struct RetryStatus
    {
        public int NumRetries;
        public long RetryStartTicks;
        public TimeSpan NextDelay;
        public Exception? LastError;
    }
}
