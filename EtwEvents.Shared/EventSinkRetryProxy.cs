using KdSoft.EtwLogging;
using Microsoft.Extensions.Logging;

namespace KdSoft.EtwEvents
{
    /// <summary>
    /// Proxy for <see cref="IEventSink"/> that handles sink failures on WriteAsync()
    /// by closing/disposing of the event sink, and re-creating it on the next call to WriteAsync().
    /// </summary>
    public class EventSinkRetryProxy: IEventSink, IEventSinkStatus<RetryStatus>
    {
        readonly string _sinkId;
        readonly string _options;
        readonly string _credentials;
        readonly IEventSinkFactory _sinkFactory;
        readonly EventSinkLoadContext? _loadContext;
        readonly string _siteName;
        readonly ILoggerFactory _loggerFactory;
        readonly ILogger<EventSinkRetryProxy> _logger;
        readonly AsyncRetrier<bool> _retrier;
        readonly ValueHolder<RetryStatus> _retryHolder;
        readonly TaskCompletionSource<bool> _tcs;

        IEventSink? _sink;
        int _disposing;

        public string SinkId => _sinkId;

        public readonly TimeSpan EventSinkTimeout = TimeSpan.FromSeconds(10);

        #region Construction & Disposal

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="sinkId">Event sink Id.</param>
        /// <param name="options">Event sink options.</param>
        /// <param name="credentials">Event sink credentials.</param>
        /// <param name="sinkFactory"><see cref="IEventSinkFactory"/> to create instances of the specified event sink.</param>
        /// <param name="loadContext">Collectible assembly load context, as we need to keep a reference to it if not null.</param>
        /// <param name="siteName">Name of source/site generating the events.</param>
        /// <param name="loggerFactory"><see cref="ILoggerFactory"/> instance needed for new event sink instances.</param>
        /// <param name="retryStrategy">Determines how retries are performed.</param>
        public EventSinkRetryProxy(
            string sinkId,
            string options,
            string credentials,
            IEventSinkFactory sinkFactory,
            EventSinkLoadContext? loadContext,
            IRetryStrategy retryStrategy,
            string siteName,
            ILoggerFactory loggerFactory
        ) {
            this._sinkId = sinkId;
            this._options = options;
            this._credentials = credentials;
            this._sinkFactory = sinkFactory;
            this._loadContext = loadContext;
            this._siteName = siteName;
            this._loggerFactory = loggerFactory;
            _tcs = new TaskCompletionSource<bool>();
            _logger = loggerFactory.CreateLogger<EventSinkRetryProxy>();
            _retrier = new AsyncRetrier<bool>(r => r, retryStrategy);
            _retryHolder = new ValueHolder<RetryStatus>();
        }

        public async ValueTask DisposeAsync() {
            GC.SuppressFinalize(this);
            _disposing = 99;
            _retrier.Stop();
            var sink = Interlocked.Exchange(ref _sink, null);
            if (sink != null) {
                var _ = sink.RunTask.ContinueWith(rt => {
                    if (rt.IsFaulted)
                        _tcs.TrySetException(rt.Exception!);
                    else if (rt.IsCanceled)
                        _tcs.TrySetCanceled();
                    else
                        _tcs.TrySetResult(rt.Result);
                });
                await sink.DisposeAsync().ConfigureAwait(false);
            }
            else {
                _tcs.TrySetResult(true);
            }
        }

        #endregion

        #region EventSink Handling

        record struct EventSinkContext(string SiteName, ILogger Logger): IEventSinkContext;

        async Task<IEventSink?> CreateEventSink() {
            var logger = _loggerFactory.CreateLogger(_sinkId);
            try {
                var sink = await _sinkFactory.Create(_options, _credentials, new EventSinkContext(_siteName, logger)).ConfigureAwait(false);
                var oldSink = Interlocked.CompareExchange(ref _sink, sink, null);
                if (oldSink != null)
                    throw new InvalidOperationException($"Must not replace EventSink instance {_sinkId} when it is not null.");
                return sink;
            }
            catch (Exception ex) {
                _retryHolder.Value.LastError = ex;
                RaiseChangedEvent();
                _logger.LogError(ex, "Error creating event sink {sinkId}.", _sinkId);
                return null;
            }
        }

        ValueTask<IEventSink?> GetSink() {
            var sink = _sink;
            if (sink != null) {
                return ValueTask.FromResult<IEventSink?>(sink);
            }
            return new ValueTask<IEventSink?>(CreateEventSink());
        }

        /*
         * When a write task fails (WriteAsync()) then we will
         * await IEventSink.RunTask and then dispose the IEventSink instance.
         * The _sink field will be set to null.
         * When a write task is executed with a null for _sink, then
         * a new IEventSink will be created, before the write task is executed.
         * Note: consider that we can only access the result of a ValueTask once!
         */

        async Task<bool> HandleFailedWrite() {
            var sink = Interlocked.Exchange(ref _sink, null);
            if (sink == null) {
                _logger.LogWarning("Failed write on null EventSink instance {eventSink}", _sinkId);
                return false;
            }

            try {
                await sink.RunTask.WaitAsync(EventSinkTimeout).ConfigureAwait(false);
            }
            catch (TimeoutException tex) {
                _retryHolder.Value.LastError = tex;
                _logger.LogError(tex, "Event sink {sinkId} timed out.", _sinkId);
            }
            catch (OperationCanceledException cex) {
                _retryHolder.Value.LastError = cex;
                _logger.LogError("Event sink {sinkId} was cancelled.", _sinkId);
            }
            catch (Exception ex) {
                _retryHolder.Value.LastError = ex;
                _logger.LogError(ex, "Event sink {sinkId} failed.", _sinkId);
            }
            finally {
                await sink.DisposeAsync().ConfigureAwait(false);
            }

            RaiseChangedEvent();
            return false;
        }

        async ValueTask<bool> InternalPerformAsyncAsync(ValueTask<bool> writeTask) {
            var result = await writeTask.ConfigureAwait(false);
            if (!result) {
                return await HandleFailedWrite().ConfigureAwait(false);
            }
            return result;
        }

        ValueTask<bool> InternalPerformAsync(ValueTask<bool> writeTask) {
            if (writeTask.IsCompleted) {
                var result = writeTask.GetAwaiter().GetResult();
                if (result) {
                    return ValueTask.FromResult(result);
                }
                else {
                    return new ValueTask<bool>(HandleFailedWrite());
                }
            }
            else {
                return InternalPerformAsyncAsync(writeTask);
            }
        }

        #endregion

        #region WriteAsync(EtwEventBatch evtBatch)

        async ValueTask<bool> InternalWriteAsync(ValueTask<IEventSink?> sinkTask, EtwEventBatch evtBatch) {
            var sink = await sinkTask.ConfigureAwait(false);
            if (sink is null)
                return false;
            return await InternalPerformAsync(sink.WriteAsync(evtBatch)).ConfigureAwait(false);
        }

        /// <summary>
        /// Called by the AsyncRetrier.
        /// </summary>
        ValueTask<bool> WriteBatchAsync(EtwEventBatch evtBatch, int retryNum, TimeSpan delay) {
            if (retryNum > 0)
                _logger.LogInformation("WriteAsync (batch) retry: {retryNum}, {delay}", retryNum, delay);
            var sinkTask = GetSink();
            if (sinkTask.IsCompleted) {
                var sink = sinkTask.GetAwaiter().GetResult();
                if (sink is null)
                    return ValueTask.FromResult(false);
                return InternalPerformAsync(sink.WriteAsync(evtBatch));
            }
            return InternalWriteAsync(sinkTask, evtBatch);
        }

        #endregion

        #region IEventSink

        public Task<bool> RunTask => _tcs.Task;

        public ValueTask<bool> WriteAsync(EtwEventBatch evtBatch) {
            if (_disposing > 0)
                return ValueTask.FromResult(false);

            if (_retryHolder.Value != _cleanStatus) {
                _retryHolder.Reset();
                RaiseChangedEvent();
            }
            return _retrier.ExecuteAsync(WriteBatchAsync, evtBatch, _retryHolder);
        }

        #endregion

        #region IEventSinkStatus

        static readonly RetryStatus _cleanStatus = new();

        public RetryStatus Status => _retryHolder.Value;

        public event Action? Changed;

        protected virtual void RaiseChangedEvent() {
            Changed?.Invoke();
        }

        #endregion
    }
}
