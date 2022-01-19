using System;
using System.Threading.Tasks;
using KdSoft.EtwEvents;
using KdSoft.EtwLogging;

namespace EtwEvents.Tests
{
    class MockSink: IEventSink
    {
        readonly int _maxWrites;
        readonly TimeSpan _duration;
        readonly TaskCompletionSource<bool> _tcs;

        int _writeCount;

        public MockSink(MockSinkOptions options) {
            var activeCycle = options.LifeCycles[options.ActiveCycle];
            _maxWrites = activeCycle.maxWrites;
            _duration = activeCycle.duration;
            _tcs = new TaskCompletionSource<bool>();
        }

        public Task<bool> RunTask => _tcs.Task;

        public ValueTask DisposeAsync() {
            _tcs.TrySetResult(true);
            return ValueTask.CompletedTask;
        }

        public async ValueTask<bool> FlushAsync() {
            await Task.Delay(_duration);
            if (++_writeCount > _maxWrites) {
                _tcs.TrySetException(new Exception("Flush Error"));
                return false;
            }
            return true;
        }

        public async ValueTask<bool> WriteAsync(EtwEvent evt) {
            await Task.Delay(_duration);
            if (++_writeCount > _maxWrites) {
                _tcs.TrySetException(new Exception("EtwEvent Error"));
                return false;
            }
            return true;
        }

        public async ValueTask<bool> WriteAsync(EtwEventBatch evtBatch) {
            await Task.Delay(_duration);
            if (++_writeCount > _maxWrites) {
                _tcs.TrySetException(new Exception("EtwEventBatch Error"));
                return false;
            }
            return true;
        }
    }
}
