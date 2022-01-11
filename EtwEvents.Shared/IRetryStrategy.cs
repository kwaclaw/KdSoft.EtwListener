namespace KdSoft.EtwEvents
{
    public interface IRetryStrategy
    {
        void Reset();

        bool NextDelay(out TimeSpan delay, out int count);
    }
}
