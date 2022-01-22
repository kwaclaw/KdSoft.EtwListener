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
        readonly DateTimeOffset _startTime;
        readonly MockSinkLifeCycle _lifeCycle;

        int _writeCount;
        EtwEvent? _event;

        public MockSink(MockSinkOptions options, MockSinkLifeCycle lifeCycle) {
            var activeCycle = options.LifeCycles[options.ActiveCycle];
            _maxWrites = activeCycle.Item1;
            _duration = activeCycle.Item2;

            this._lifeCycle = lifeCycle;
            lifeCycle.Sink = this;
            lifeCycle.MaxWrites = _maxWrites;
            lifeCycle.CallDuration = _duration;
            _startTime = DateTimeOffset.UtcNow;

            _tcs = new TaskCompletionSource<bool>();
        }

        public Task<bool> RunTask => _tcs.Task;

        public ValueTask DisposeAsync() {
            CompleteLifeCycle();
            _lifeCycle.Disposed = true;
            _tcs.TrySetResult(true);
            return ValueTask.CompletedTask;
        }

        void CompleteLifeCycle() {
            _lifeCycle.LifeSpan = DateTimeOffset.UtcNow - _startTime;
            _lifeCycle.WriteCount = _writeCount;
            if (_event != null)
                _lifeCycle.Event = _event;
        }

        public async ValueTask<bool> FlushAsync() {
            if (_writeCount >= _maxWrites) {
                CompleteLifeCycle();
                _tcs.TrySetException(new Exception("Flush Error"));
                return false;
            }
            await Task.Delay(_duration);
            _writeCount++;
            return true;
        }

        public async ValueTask<bool> WriteAsync(EtwEvent evt) {
            _event = evt;
            if (_writeCount >= _maxWrites) {
                CompleteLifeCycle();
                _tcs.TrySetException(new Exception("EtwEvent Error"));
                return false;
            }
            await Task.Delay(_duration);
            _writeCount++;
            return true;
        }

        public async ValueTask<bool> WriteAsync(EtwEventBatch evtBatch) {
            if (_writeCount >= _maxWrites) {
                CompleteLifeCycle();
                _tcs.TrySetException(new Exception("EtwEventBatch Error"));
                return false;
            }
            await Task.Delay(_duration);
            _writeCount++;
            return true;
        }
    }

    class MockSinkLifeCycle
    {
        public MockSink? Sink { get; set; }
        public EtwEvent? Event { get; set; }
        public int WriteCount { get; set; }
        public int MaxWrites { get; set; }
        public TimeSpan CallDuration { get; set; }
        public TimeSpan LifeSpan { get; set; }
        public bool Disposed { get; set; }
    }
}
