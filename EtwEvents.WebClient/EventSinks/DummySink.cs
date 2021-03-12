using System.Threading.Tasks;
using KdSoft.EtwEvents.Client.Shared;
using KdSoft.EtwLogging;

namespace KdSoft.EtwEvents.WebClient.EventSinks {
    sealed class DummySink: IEventSink
    {
        public Task<bool> RunTask => Task<bool>.FromResult(true);

        public void Dispose() {
            //
        }

        public ValueTask DisposeAsync() {
            return default;
        }

        public ValueTask<bool> FlushAsync() {
            return new ValueTask<bool>(true);
        }

        public ValueTask<bool> WriteAsync(EtwEvent evt, long sequenceNo) {
            return new ValueTask<bool>(true);
        }

        public ValueTask<bool> WriteAsync(EtwEventBatch evtBatch, long sequenceNo) {
            return new ValueTask<bool>(true);
        }
    }
}
