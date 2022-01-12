using KdSoft.EtwLogging;
using Microsoft.Extensions.Logging;

namespace KdSoft.EtwEvents
{
    /// <summary>
    /// Proxy for <see cref="IEventSink"/> that handles sink failures on FlushAsync() or WriteAsync()
    /// by closing/disposing of the event sink, and re-creating it on the next call to FlushAsync() or WriteAsync().
    /// </summary>
    public class EventSinkRetryProxy: IEventSink {
        readonly string _sinkId;
        readonly string _options;
        readonly string _credentials;
        readonly IEventSinkFactory _sinkFactory;
        readonly ILoggerFactory _loggerFactory;
        readonly ILogger<EventSinkRetryProxy> _logger;
        readonly AsyncRetrier<bool> _retrier;

        IEventSink? _sink;

        //TODO Runtask?

        public Task<bool> RunTask => throw new NotImplementedException();

        #region Construction & Disposal

        public EventSinkRetryProxy(string sinkId, string options, string credentials, IEventSinkFactory sinkFactory, ILoggerFactory loggerFactory) {
            this._sinkId = sinkId;
            this._options = options;
            this._credentials = credentials;
            this._sinkFactory = sinkFactory;
            this._loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<EventSinkRetryProxy>();
            var retryStrategy = new BackoffRetryStrategy(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(60), TimeSpan.FromMinutes(15), true);
            _retrier = new AsyncRetrier<bool>(r => r, retryStrategy);
        }

        public async ValueTask DisposeAsync() {
            var sink = Interlocked.Exchange(ref _sink, null);
            if (sink != null) {
                await sink.FlushAsync().ConfigureAwait(false);
                await sink.DisposeAsync().ConfigureAwait(false);
            }
        }

        #endregion

        #region EventSink Handling

        async Task<IEventSink> CreateEventSink() {
            var logger = _loggerFactory.CreateLogger(_sinkId);
            var sink = await _sinkFactory.Create(_options, _credentials, logger).ConfigureAwait(false);
            var oldSink = Interlocked.CompareExchange(ref _sink, sink, null);
            if (oldSink != null)
                throw new InvalidOperationException($"Must not replace EventSink instance {_sinkId} when it is not null.");
            return sink;
        }

        ValueTask<IEventSink> GetSink() {
            var sink = _sink;
            if (sink != null) {
                return ValueTask.FromResult(sink);
            }
            return new ValueTask<IEventSink>(CreateEventSink());
        }

        /*
         * When a write task fails (FlushAsync() or WriteAsync()) then we will
         * await IEventSink.RunTask and then dispose the IEventSink instance.
         * The _sink field will be set to null.
         * When a write task is executed with a null for _sink, then
         * a new IEventSink will be created, before the write task is executed.
         * Note: consider that we can only access the result of a ValueTask once!
         */

        async Task<bool> HandleFailedWrite() {
            var sink = Interlocked.Exchange(ref _sink, null);
            if (sink == null)
                throw new InvalidOperationException($"EventSink instance {_sinkId} is null.");
            try {
                await sink.RunTask.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            }
            catch (TimeoutException tex) {
                _logger.LogError(tex, "Event sink {sinkId} timed out.", _sinkId);
            }
            catch (OperationCanceledException) {
                _logger.LogError("Event sink {sinkId} was cancelled.", _sinkId);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Event sink {sinkId} failed.", _sinkId);
            }
            finally {
                await sink.DisposeAsync().ConfigureAwait(false);
            }
            return false;
        }

        async ValueTask<bool> InternalPerformAsyncAsync(ValueTask<bool> writeTask) {
            var result = await writeTask.ConfigureAwait(false);
            if (!result)
                return await HandleFailedWrite().ConfigureAwait(false);
            return result;
        }

        ValueTask<bool> InternalPerformAsync(ValueTask<bool> writeTask) {
            if (writeTask.IsCompleted) {
                var result = writeTask.GetAwaiter().GetResult();
                return result ? ValueTask.FromResult(result) : new ValueTask<bool>(HandleFailedWrite());
            }
            else {
                return InternalPerformAsyncAsync(writeTask);
            }
        }

        #endregion

        #region FlushAsync

        async ValueTask<bool> InternalFlushAsync(ValueTask<IEventSink> sinkTask) {
            var sink = await sinkTask.ConfigureAwait(false);
            return await InternalPerformAsync(sink.FlushAsync()).ConfigureAwait(false);
        }

        ValueTask<bool> DoFlushAsync() {
            var sinkTask = GetSink();
            if (sinkTask.IsCompleted) {
                var sink = sinkTask.GetAwaiter().GetResult();
                return InternalPerformAsync(sink.FlushAsync());
            }
            return InternalFlushAsync(sinkTask);
        }

        public ValueTask<bool> FlushAsync() {
            return _retrier.ExecuteAsync(DoFlushAsync);
        }

        #endregion

        #region WriteAsync(EtwEvent)

        async ValueTask<bool> InternalWriteAsync(ValueTask<IEventSink> sinkTask, EtwEvent evt) {
            var sink = await sinkTask.ConfigureAwait(false);
            return await InternalPerformAsync(sink.WriteAsync(evt)).ConfigureAwait(false);
        }

        ValueTask<bool> WriteEventAsync(EtwEvent evt) {
            var sinkTask = GetSink();
            if (sinkTask.IsCompleted) {
                var sink = sinkTask.GetAwaiter().GetResult();
                return InternalPerformAsync(sink.WriteAsync(evt));
            }
            return InternalWriteAsync(sinkTask, evt);
        }

        public ValueTask<bool> WriteAsync(EtwEvent evt) {
            return _retrier.ExecuteAsync(WriteEventAsync, evt);
        }

        #endregion

        #region WriteAsync(EtwEventBatch evtBatch)

        async ValueTask<bool> InternalWriteAsync(ValueTask<IEventSink> sinkTask, EtwEventBatch evtBatch) {
            var sink = await sinkTask.ConfigureAwait(false);
            return await InternalPerformAsync(sink.WriteAsync(evtBatch)).ConfigureAwait(false);
        }

        ValueTask<bool> WriteBatchAsync(EtwEventBatch evtBatch) {
            var sinkTask = GetSink();
            if (sinkTask.IsCompleted) {
                var sink = sinkTask.GetAwaiter().GetResult();
                return InternalPerformAsync(sink.WriteAsync(evtBatch));
            }
            return InternalWriteAsync(sinkTask, evtBatch);
        }

        public ValueTask<bool> WriteAsync(EtwEventBatch evtBatch) {
            return _retrier.ExecuteAsync(WriteBatchAsync, evtBatch);
        }

        #endregion
    }
}
