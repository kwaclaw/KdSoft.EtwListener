using KdSoft.EtwLogging;

namespace KdSoft.EtwEvents
{
    public sealed class NullSink: IEventSink
    {
        public Task<bool> RunTask => Task.FromResult(true);

        public void Dispose() {
            //
        }

        public ValueTask DisposeAsync() {
            return default;
        }

        public ValueTask<bool> WriteAsync(EtwEventBatch evtBatch) {
            return ValueTask.FromResult(true);
        }
    }
}
