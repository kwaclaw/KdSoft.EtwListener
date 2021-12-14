using System.Threading.Tasks;
using KdSoft.EtwLogging;

namespace KdSoft.EtwEvents.Client
{
    public sealed class DummySink: IEventSink
    {
        public Task RunTask => Task.CompletedTask;

        public void Dispose() {
            //
        }

        public ValueTask DisposeAsync() {
            return default;
        }

        public ValueTask<bool> FlushAsync() {
            return new ValueTask<bool>(true);
        }

        public ValueTask<bool> WriteAsync(EtwEvent evt) {
            return new ValueTask<bool>(true);
        }

        public ValueTask<bool> WriteAsync(EtwEventBatch evtBatch) {
            return new ValueTask<bool>(true);
        }
    }
}
