namespace KdSoft.EtwEvents
{
    public class AsyncRetryExecutor<T>
    {
        protected readonly Predicate<T> _succeeded;
        protected readonly IRetryStrategy _retryStrategy;

        public AsyncRetryExecutor(Predicate<T> succeeded, IRetryStrategy retryStrategy) {
            this._succeeded = succeeded;
            this._retryStrategy = retryStrategy;
        }

        #region No Arguments

        async ValueTask<T> ExecuteAsyncAsync(ValueTask<T> task, Func<ValueTask<T>> callback) {
            var result = await task.ConfigureAwait(false);
            while (!_succeeded(result)) {
                if (_retryStrategy.NextDelay(out var delay, out var count)) {
                    await Task.Delay(delay).ConfigureAwait(false);
                    result = await callback().ConfigureAwait(false);
                }
                return result;
            }
            return result;
        }

        public ValueTask<T> ExecuteAsync(Func<ValueTask<T>> callback) {
            // check fast path (sync completion)
            var task = callback();
            if (task.IsCompleted) {
                var result = task.GetAwaiter().GetResult();
                if (_succeeded(result)) {
                    return ValueTask.FromResult(result);
                }
            }
            // otherwise go full async
            _retryStrategy.Reset();
            return ExecuteAsyncAsync(task, callback);
        }

        #endregion

        #region One Argument

        async ValueTask<T> ExecuteAsyncAsync<P>(ValueTask<T> task, Func<P, ValueTask<T>> callback, P arg) {
            var result = await task.ConfigureAwait(false);
            while (!_succeeded(result)) {
                if (_retryStrategy.NextDelay(out var delay, out var count)) {
                    await Task.Delay(delay).ConfigureAwait(false);
                    result = await callback(arg).ConfigureAwait(false);
                }
                return result;
            }
            return result;
        }

        public ValueTask<T> ExecuteAsync<P>(Func<P, ValueTask<T>> callback, P arg) {
            // check fast path (sync completion)
            var task = callback(arg);
            if (task.IsCompleted) {
                var result = task.GetAwaiter().GetResult();
                if (_succeeded(result)) {
                    return ValueTask.FromResult(result);
                }
            }
            // otherwise go full async
            _retryStrategy.Reset();
            return ExecuteAsyncAsync(task, callback, arg);
        }

        #endregion

        #region Two Arguments

        async ValueTask<T> ExecuteAsyncAsync<P, Q>(ValueTask<T> task, Func<P, Q, ValueTask<T>> callback, P argP, Q argQ) {
            var result = await task.ConfigureAwait(false);
            while (!_succeeded(result)) {
                if (_retryStrategy.NextDelay(out var delay, out var count)) {
                    await Task.Delay(delay).ConfigureAwait(false);
                    result = await callback(argP, argQ).ConfigureAwait(false);
                }
                return result;
            }
            return result;
        }

        public ValueTask<T> ExecuteAsync<P, Q>(Func<P, Q, ValueTask<T>> callback, P argP, Q argQ) {
            // check fast path (sync completion)
            var task = callback(argP, argQ);
            if (task.IsCompleted) {
                var result = task.GetAwaiter().GetResult();
                if (_succeeded(result)) {
                    return ValueTask.FromResult(result);
                }
            }
            // otherwise go full async
            _retryStrategy.Reset();
            return ExecuteAsyncAsync(task, callback, argP, argQ);
        }

        #endregion
    }
}
