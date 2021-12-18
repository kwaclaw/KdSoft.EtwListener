using Grpc.Core;
using KdSoft.EtwLogging;
using Microsoft.Extensions.Logging;

namespace KdSoft.EtwEvents.EventSinks
{
    public class gRPCSink: IEventSink
    {
        readonly AsyncClientStreamingCall<EtwEventBatch, EtwEventResponse> _eventStream;
        readonly ILogger _logger;
        readonly TaskCompletionSource<bool> _tcs;

        readonly EtwEventBatch _pendingBatch;

        EtwEventBatch? _currentBatch;
        int _isDisposed = 0;

        public Task<bool> RunTask { get; }

        public gRPCSink(AsyncClientStreamingCall<EtwEventBatch, EtwEventResponse> eventStream, ILogger logger) {
            this._eventStream = eventStream;
            this._logger = logger;

            _tcs = new TaskCompletionSource<bool>();
            RunTask = _tcs.Task;

            eventStream.ResponseAsync.ContinueWith(res => {
                if (res.IsFaulted) {
                    _tcs.TrySetException(res.Exception!);
                }
                else if (res.IsCanceled) {
                    _tcs.TrySetCanceled();
                }
                _tcs.TrySetResult(false);
            });

            this._pendingBatch = new EtwEventBatch();
        }

        public bool IsDisposed {
            get {
                Interlocked.MemoryBarrier();
                var isDisposed = this._isDisposed;
                Interlocked.MemoryBarrier();
                return isDisposed > 0;
            }
        }

        public void Dispose() {
            var oldDisposed = Interlocked.CompareExchange(ref _isDisposed, 99, 0);
            if (oldDisposed == 0) {
                try {
                    _eventStream.Dispose();
                }
                catch (Exception ex) {
                    _logger.LogError(ex, "Error closing event stream '{eventSink)}'.", nameof(gRPCSink));
                }
                _tcs.TrySetResult(true);
            }
        }

        // Warning: ValueTasks should not be awaited multiple times
        public ValueTask DisposeAsync() {
            Dispose();
            return default;
        }

        /// <summary>
        /// Flushes internal buffer, sending the buffered events.
        /// </summary>
        /// <returns></returns>
        public async ValueTask<bool> FlushAsync() {
            if (IsDisposed || RunTask.IsCompleted)
                return false;
            try {
                // if _currentBatch points to _pendingBatch, then send it and clear it
                var flushBatch = Interlocked.CompareExchange(ref _currentBatch, null, _pendingBatch);
                if (object.ReferenceEquals(flushBatch, _pendingBatch) && flushBatch.Events.Count > 0) {
                    await this._eventStream.RequestStream.WriteAsync(flushBatch).ConfigureAwait(false);
                    flushBatch.Events.Clear();
                }
            }
            catch (Exception ex) {
                _tcs.TrySetException(ex);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Writes evt to internal buffer, events are not sent until FlushAsync() is called.
        /// </summary>
        /// <param name="evt"></param>
        /// <returns></returns>
        public ValueTask<bool> WriteAsync(EtwEvent evt) {
            if (IsDisposed || RunTask.IsCompleted)
                return ValueTask.FromResult(false);
            try {
                // if _currentBatch is null, update it to _pendingBatch, otherwise leave it alone
                var oldBatch = Interlocked.CompareExchange(ref _currentBatch, _pendingBatch, null);
                if (object.ReferenceEquals(oldBatch, null)) {
                    _currentBatch.Events.Clear();
                }
                _currentBatch.Events.Add(evt);
                return ValueTask.FromResult(true);
            }
            catch (Exception ex) {
                _tcs.TrySetException(ex);
                return ValueTask.FromResult(false);
            }
        }

        /// <summary>
        /// Sends evtBatch and any pending events.
        /// </summary>
        /// <param name="evtBatch"></param>
        /// <returns></returns>
        public async ValueTask<bool> WriteAsync(EtwEventBatch evtBatch) {
            if (IsDisposed || RunTask.IsCompleted)
                return false;
            try {
                // if _currentBatch points to _pendingBatch, then clear it and add the new evtBatch
                // to the end of _pendingBatch, then send _pendingBatch
                var sendBatch = Interlocked.CompareExchange(ref _currentBatch, null, _pendingBatch);
                if (object.ReferenceEquals(sendBatch, _pendingBatch)) {
                    sendBatch.Events.AddRange(evtBatch.Events);
                }
                else {
                    sendBatch = evtBatch;
                }

                await this._eventStream.RequestStream.WriteAsync(evtBatch).ConfigureAwait(false);
                if (object.ReferenceEquals(sendBatch, _pendingBatch)) {
                    sendBatch.Events.Clear();
                }
            }
            catch (Exception ex) {
                _tcs.TrySetException(ex);
                return false;
            }
            return true;
        }
    }
}
