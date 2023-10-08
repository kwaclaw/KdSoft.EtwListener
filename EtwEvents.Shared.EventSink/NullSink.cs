using KdSoft.EtwLogging;

namespace KdSoft.EtwEvents
{
    public sealed class NullSink: IEventSink
    {
        public Task<bool> RunTask => Task.FromResult(true);

#pragma warning disable CA1822 // Mark members as static
        public void Dispose() {
            //
        }
#pragma warning restore CA1822 // Mark members as static

        public ValueTask DisposeAsync() {
            return default;
        }

        public ValueTask<bool> WriteAsync(EtwEventBatch evtBatch) {
            return ValueTask.FromResult(true);
        }
    }
}
