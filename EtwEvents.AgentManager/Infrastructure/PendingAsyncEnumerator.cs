namespace KdSoft.EtwEvents
{
    public abstract class PendingAsyncEnumerator<T>: IAsyncEnumerator<T> where T : notnull
    {
        readonly CancellationToken _cancelToken;
        readonly ManualResetValueTaskSource<bool> _vts;
        readonly object _taskSync = new();

        public PendingAsyncEnumerator(CancellationToken cancelToken) {
            this._cancelToken = cancelToken;
            this._vts = new ManualResetValueTaskSource<bool>();
            this._version = _vts.Version;
        }

        bool _pending;
        short _version;

        // can be  called from any thread anytime
        public ValueTask Advance() {
            bool doGetNext = false;

            lock (_taskSync) {
                if (_vts.Version != _version) {
                    _pending = false;
                    _version = _vts.Version;
                    doGetNext = true;
                }
                else {
                    _pending = true;
                }
            }

            if (doGetNext) {
                var getTask = GetNext().ContinueWith(gnt => {
                    Current = gnt.Result;
                    _vts.SetResult(true);
                }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                return new ValueTask(getTask);
            }

            return default;
        }

        public T Current { get; private set; } = default!;

        public abstract ValueTask DisposeAsync();

        protected abstract Task<T> GetNext();

        /// <summary>
        /// Like IEnumerator.MoveNext(), but must not be called concurrently with itself, or DisposeAsync().
        /// </summary>
        public ValueTask<bool> MoveNextAsync() {
            if (_cancelToken.IsCancellationRequested)
                return new ValueTask<bool>(false);

            bool doGetNext = false;

            lock (_taskSync) {
                if (_pending) {
                    _pending = false;
                    doGetNext = true;
                }
                else {
                    _vts.Reset();
                }
            }

            if (doGetNext) {
                var getTask = GetNext().ContinueWith<bool>(gnt => {
                    Current = gnt.Result;
                    return true;
                }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Current);
                return new ValueTask<bool>(getTask);
            }

            return new ValueTask<bool>(_vts, _vts.Version);
        }
    }
}
