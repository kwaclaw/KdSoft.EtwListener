namespace KdSoft.EtwEvents
{
    public class AsyncRetrier<T>
    {
        protected readonly Predicate<T> _succeeded;
        protected readonly IRetryStrategy _retryStrategy;
        int _stop;

        public AsyncRetrier(Predicate<T> succeeded, IRetryStrategy retryStrategy) {
            this._succeeded = succeeded;
            this._retryStrategy = retryStrategy;
        }

        /// <summary>
        /// Stops the retry loop. Note: call <see cref="Reset()"/> to start using the AsyncRetrier again.
        /// </summary>
        public void Stop() {
            _stop = 99;
        }

        /// <summary>
        /// Resets the  so it can be re-used again after it was stopped.
        /// </summary>
        public void Reset() {
            _stop = 0;
        }

        public bool IsStopped => _stop != 0;

        #region No Arguments

        async ValueTask<T> ExecuteAsyncAsync(
            ValueTask<T> task,
            Func<int, TimeSpan, ValueTask<T>> callback,
            ValueHolder<RetryStatus>? retryHolder
        ) {
            var result = await task.ConfigureAwait(false);
            while (!_succeeded(result)) {
                if (_stop != 0)
                    return result;
                if (_retryStrategy.NextDelay(out var delay, out var count)) {
                    if (retryHolder != null) {
                        retryHolder.Value.NumRetries = count;
                        retryHolder.Value.NextDelay = delay;
                        // make sure access to  retryHolder.Value.RetryStartTicks is thread-safe
                        Interlocked.Exchange(ref retryHolder.Value.RetryStartTicks, DateTimeOffset.UtcNow.Ticks);
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

        /// <summary>
        /// Executes callback in a retry loop based on the retry strategy.
        /// </summary>
        /// <param name="callback">Callback to retry.</param>
        /// <param name="retryHolder">Holds retry information.</param>
        /// <returns>Callback result.</returns>
        public ValueTask<T> ExecuteAsync(Func<int, TimeSpan, ValueTask<T>> callback, ValueHolder<RetryStatus>? retryHolder = null) {
            var task = callback(0, default);

            // use this so we won't re-use task, as it is a ValueTask
            ValueTask<T> resultTask;

            // check fast path (sync completion)
            if (task.IsCompletedSuccessfully) {
                var result = task.Result;
                resultTask = ValueTask.FromResult(result);
                if (_succeeded(result)) {
                    return resultTask;
                }
            }
            else {
                resultTask = task;
            }

            // otherwise go full async
            _retryStrategy.Reset();
            return ExecuteAsyncAsync(resultTask, callback, retryHolder);
        }

        #endregion

        #region One Argument

        async ValueTask<T> ExecuteAsyncAsync<P>(
            ValueTask<T> task,
            Func<P, int, TimeSpan, ValueTask<T>> callback,
            P arg,
            ValueHolder<RetryStatus>? retryHolder
        ) {
            var result = await task.ConfigureAwait(false);
            while (!_succeeded(result)) {
                if (_stop != 0)
                    return result;
                if (_retryStrategy.NextDelay(out var delay, out var count)) {
                    if (retryHolder != null) {
                        retryHolder.Value.NumRetries = count;
                        retryHolder.Value.NextDelay = delay;
                        // make sure access to  retryHolder.Value.RetryStartTicks is thread-safe
                        Interlocked.Exchange(ref retryHolder.Value.RetryStartTicks, DateTimeOffset.UtcNow.Ticks);
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

        /// <summary>
        /// Executes callback in a retry loop based on the retry strategy.
        /// </summary>
        /// <typeparam name="P"></typeparam>
        /// <param name="callback">Callback to retry.</param>
        /// <param name="arg">Argument passed to callback.</param>
        /// <param name="retryHolder">Holds retry information.</param>
        /// <returns></returns>
        public ValueTask<T> ExecuteAsync<P>(
            Func<P, int, TimeSpan, ValueTask<T>> callback,
            P arg,
            ValueHolder<RetryStatus>? retryHolder = null
        ) {
            var task = callback(arg, 0, default);

            // use this so we won't re-use task, as it is a ValueTask
            ValueTask<T> resultTask;

            // check fast path (sync completion)
            if (task.IsCompleted) {
                var result = task.Result;
                resultTask = ValueTask.FromResult(result);
                if (_succeeded(result)) {
                    return resultTask;
                }
            }
            else {
                resultTask = task;
            }

            // otherwise go full async
            _retryStrategy.Reset();
            return ExecuteAsyncAsync(resultTask, callback, arg, retryHolder);
        }

        #endregion

        #region Two Arguments

        async ValueTask<T> ExecuteAsyncAsync<P, Q>(
            ValueTask<T> task,
            Func<P, Q, int, TimeSpan, ValueTask<T>> callback,
            P argP,
            Q argQ,
            ValueHolder<RetryStatus>? retryHolder
        ) {
            var result = await task.ConfigureAwait(false);
            while (!_succeeded(result)) {
                if (_stop != 0)
                    return result;
                if (_retryStrategy.NextDelay(out var delay, out var count)) {
                    if (retryHolder != null) {
                        retryHolder.Value.NumRetries = count;
                        retryHolder.Value.NextDelay = delay;
                        // make sure access to  retryHolder.Value.RetryStartTicks is thread-safe
                        Interlocked.Exchange(ref retryHolder.Value.RetryStartTicks, DateTimeOffset.UtcNow.Ticks);
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

        /// <summary>
        /// Executes callback in a retry loop based on the retry strategy.
        /// </summary>
        /// <typeparam name="P"></typeparam>
        /// <typeparam name="Q"></typeparam>
        /// <param name="callback">Callback to retry.</param>
        /// <param name="argP">First argument passed to callback.</param>
        /// <param name="argQ">Secoind argument passed to callback.</param>
        /// <param name="retryHolder">Holds retry information.</param>
        /// <returns></returns>
        public ValueTask<T> ExecuteAsync<P, Q>(
            Func<P, Q, int, TimeSpan, ValueTask<T>> callback,
            P argP,
            Q argQ,
            ValueHolder<RetryStatus>? retryHolder = null
        ) {
            var task = callback(argP, argQ, 0, default);

            // use this so we won't re-use task, as it is a ValueTask
            ValueTask<T> resultTask;

            // check fast path (sync completion)
            if (task.IsCompleted) {
                var result = task.Result;
                resultTask = ValueTask.FromResult(result);
                if (_succeeded(result)) {
                    return resultTask;
                }
            }
            else {
                resultTask = task;
            }

            // otherwise go full async
            _retryStrategy.Reset();
            return ExecuteAsyncAsync(resultTask, callback, argP, argQ, retryHolder);
        }

        #endregion
    }
}
