using KdSoft.EtwLogging;

namespace KdSoft.EtwEvents.Tests
{
    class MockSink: IEventSink
    {
        readonly int _maxWrites;
        readonly TimeSpan _duration;
        readonly TaskCompletionSource<bool> _tcs;
        readonly MockSinkLifeCycle _lifeCycle;

        int _writeCount;
        EtwEvent? _event;
        int _completed;

        public MockSink(MockSinkOptions options, MockSinkLifeCycle lifeCycle) {
            var activeCycle = options.LifeCycles[options.ActiveCycle];
            _maxWrites = activeCycle.Item1;
            _duration = activeCycle.Item2;

            this._lifeCycle = lifeCycle;
            lifeCycle.Sink = this;
            lifeCycle.MaxWrites = _maxWrites;
            lifeCycle.CallDuration = _duration;

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
            var oldCompleted = Interlocked.Exchange(ref _completed, 99);
            if (oldCompleted > 0)
                return;
            _lifeCycle.WriteCount = _writeCount;
            if (_event != null)
                _lifeCycle.Event = _event;
        }

        public async ValueTask<bool> WriteAsync(EtwEventBatch evtBatch) {
            _writeCount++;
            _event = evtBatch.Events[0];
            if (_writeCount > _maxWrites) {
                CompleteLifeCycle();
                _tcs.TrySetException(new Exception("EtwEventBatch Error"));
                return false;
            }
            await Task.Delay(_duration);
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
        public bool Disposed { get; set; }
    }
}
