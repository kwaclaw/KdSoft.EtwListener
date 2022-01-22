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

        async ValueTask<T> ExecuteAsyncAsync(
            ValueTask<T> task,
            Func<int, TimeSpan, ValueTask<T>> callback,
            ValueHolder<int, TimeSpan>? retryHolder
        ) {
            var result = await task.ConfigureAwait(false);
            while (!_succeeded(result)) {
                if (_retryStrategy.NextDelay(out var delay, out var count)) {
                    if (retryHolder != null) {
                        retryHolder.Value1 = count;
                        retryHolder.Value2 = delay;
                    }
                    await Task.Delay(delay).ConfigureAwait(false);
                    result = await callback(count, delay).ConfigureAwait(false);
                }
                else {
                    return result;
                }
            }
            return result;
        }

        public ValueTask<T> ExecuteAsync(Func<int, TimeSpan, ValueTask<T>> callback, ValueHolder<int, TimeSpan>? retryHolder = null) {
            // check fast path (sync completion)
            var task = callback(0, default);
            if (task.IsCompleted) {
                var result = task.GetAwaiter().GetResult();
                if (_succeeded(result)) {
                    return ValueTask.FromResult(result);
                }
            }
            // otherwise go full async
            _retryStrategy.Reset();
            return ExecuteAsyncAsync(task, callback, retryHolder);
        }

        #endregion

        #region One Argument

        async ValueTask<T> ExecuteAsyncAsync<P>(
            ValueTask<T> task,
            Func<P, int, TimeSpan, ValueTask<T>> callback,
            P arg,
            ValueHolder<int, TimeSpan>? retryHolder
        ) {
            var result = await task.ConfigureAwait(false);
            while (!_succeeded(result)) {
                if (_retryStrategy.NextDelay(out var delay, out var count)) {
                    if (retryHolder != null) {
                        retryHolder.Value1 = count;
                        retryHolder.Value2 = delay;
                    }
                    await Task.Delay(delay).ConfigureAwait(false);
                    result = await callback(arg, count, delay).ConfigureAwait(false);
                }
                else {
                    return result;
                }
            }
            return result;
        }

        public ValueTask<T> ExecuteAsync<P>(
            Func<P, int, TimeSpan, ValueTask<T>> callback,
            P arg,
            ValueHolder<int, TimeSpan>? retryHolder = null
        ) {
            // check fast path (sync completion)
            var task = callback(arg, 0, default);
            if (task.IsCompleted) {
                var result = task.GetAwaiter().GetResult();
                if (_succeeded(result)) {
                    return ValueTask.FromResult(result);
                }
            }
            // otherwise go full async
            _retryStrategy.Reset();
            return ExecuteAsyncAsync(task, callback, arg, retryHolder);
        }

        #endregion

        #region Two Arguments

        async ValueTask<T> ExecuteAsyncAsync<P, Q>(
            ValueTask<T> task,
            Func<P, Q, int, TimeSpan, ValueTask<T>> callback,
            P argP,
            Q argQ,
            ValueHolder<int, TimeSpan>? retryHolder
        ) {
            var result = await task.ConfigureAwait(false);
            while (!_succeeded(result)) {
                if (_retryStrategy.NextDelay(out var delay, out var count)) {
                    if (retryHolder != null) {
                        retryHolder.Value1 = count;
                        retryHolder.Value2 = delay;
                    }
                    await Task.Delay(delay).ConfigureAwait(false);
                    result = await callback(argP, argQ, count, delay).ConfigureAwait(false);
                }
                else {
                    return result;
                }
            }
            return result;
        }

        public ValueTask<T> ExecuteAsync<P, Q>(
            Func<P, Q, int, TimeSpan, ValueTask<T>> callback,
            P argP,
            Q argQ,
            ValueHolder<int, TimeSpan>? retryHolder = null
        ) {
            // check fast path (sync completion)
            var task = callback(argP, argQ, 0, default);
            if (task.IsCompleted) {
                var result = task.GetAwaiter().GetResult();
                if (_succeeded(result)) {
                    return ValueTask.FromResult(result);
                }
            }
            // otherwise go full async
            _retryStrategy.Reset();
            return ExecuteAsyncAsync(task, callback, argP, argQ, retryHolder);
        }

        #endregion
    }
}
