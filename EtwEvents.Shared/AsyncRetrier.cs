namespace KdSoft.EtwEvents
{
    public class AsyncRetrier<T>
    {
        protected readonly Predicate<T> _succeeded;
        protected readonly IRetryStrategy _retryStrategy;

        public AsyncRetrier(Predicate<T> succeeded, IRetryStrategy retryStrategy) {
            this._succeeded = succeeded;
            this._retryStrategy = retryStrategy;
        }

        #region No Arguments

        async ValueTask<T> ExecuteAsyncAsync(ValueTask<T> task, Func<int, ValueTask<T>> callback, ValueHolder<int>? countHolder) {
            var result = await task.ConfigureAwait(false);
            while (!_succeeded(result)) {
                if (_retryStrategy.NextDelay(out var delay, out var count)) {
                    if (countHolder != null)
                        countHolder.Value = count;
                    await Task.Delay(delay).ConfigureAwait(false);
                    result = await callback(count).ConfigureAwait(false);
                }
                else {
                    return result;
                }
            }
            return result;
        }

        public ValueTask<T> ExecuteAsync(Func<int, ValueTask<T>> callback, ValueHolder<int>? countHolder = null) {
            // check fast path (sync completion)
            var task = callback(0);
            if (task.IsCompleted) {
                var result = task.GetAwaiter().GetResult();
                if (_succeeded(result)) {
                    return ValueTask.FromResult(result);
                }
            }
            // otherwise go full async
            _retryStrategy.Reset();
            return ExecuteAsyncAsync(task, callback, countHolder);
        }

        #endregion

        #region One Argument

        async ValueTask<T> ExecuteAsyncAsync<P>(
            ValueTask<T> task,
            Func<P, int, ValueTask<T>> callback,
            P arg,
            ValueHolder<int>? countHolder
        ) {
            var result = await task.ConfigureAwait(false);
            while (!_succeeded(result)) {
                if (_retryStrategy.NextDelay(out var delay, out var count)) {
                    if (countHolder != null)
                        countHolder.Value = count;
                    await Task.Delay(delay).ConfigureAwait(false);
                    result = await callback(arg, count).ConfigureAwait(false);
                }
                else {
                    return result;
                }
            }
            return result;
        }

        public ValueTask<T> ExecuteAsync<P>(
            Func<P, int, ValueTask<T>> callback,
            P arg,
            ValueHolder<int>? countHolder = null
        ) {
            // check fast path (sync completion)
            var task = callback(arg, 0);
            if (task.IsCompleted) {
                var result = task.GetAwaiter().GetResult();
                if (_succeeded(result)) {
                    return ValueTask.FromResult(result);
                }
            }
            // otherwise go full async
            _retryStrategy.Reset();
            return ExecuteAsyncAsync(task, callback, arg, countHolder);
        }

        #endregion

        #region Two Arguments

        async ValueTask<T> ExecuteAsyncAsync<P, Q>(
            ValueTask<T> task,
            Func<P, Q, int, ValueTask<T>> callback,
            P argP,
            Q argQ,
            ValueHolder<int>? countHolder
        ) {
            var result = await task.ConfigureAwait(false);
            while (!_succeeded(result)) {
                if (_retryStrategy.NextDelay(out var delay, out var count)) {
                    if (countHolder != null)
                        countHolder.Value = count;
                    await Task.Delay(delay).ConfigureAwait(false);
                    result = await callback(argP, argQ, count).ConfigureAwait(false);
                }
                else {
                    return result;
                }
            }
            return result;
        }

        public ValueTask<T> ExecuteAsync<P, Q>(
            Func<P, Q, int, ValueTask<T>> callback,
            P argP,
            Q argQ,
            ValueHolder<int>? countHolder = null
        ) {
            // check fast path (sync completion)
            var task = callback(argP, argQ, 0);
            if (task.IsCompleted) {
                var result = task.GetAwaiter().GetResult();
                if (_succeeded(result)) {
                    return ValueTask.FromResult(result);
                }
            }
            // otherwise go full async
            _retryStrategy.Reset();
            return ExecuteAsyncAsync(task, callback, argP, argQ, countHolder);
        }

        #endregion
    }
}
