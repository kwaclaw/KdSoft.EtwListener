using System;
using System.Threading;
using System.Threading.Tasks;
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

        int _isDisposed = 0;

        public Task<bool> RunTask { get; }

        public gRPCSink(AsyncClientStreamingCall<EtwEventBatch, EtwEventResponse> eventStream, IEventSinkContext context) {
            this._eventStream = eventStream;
            this._logger = context.Logger;

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
        public async ValueTask DisposeAsync() {
            try {
                await _eventStream.RequestStream.CompleteAsync();
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error completing event stream '{eventSink)}'.", nameof(gRPCSink));
            }
            Dispose();
        }

        /// <summary>
        /// Sends evtBatch.
        /// </summary>
        /// <param name="evtBatch"></param>
        /// <returns></returns>
        public async ValueTask<bool> WriteAsync(EtwEventBatch evtBatch) {
            if (IsDisposed || RunTask.IsCompleted)
                return false;
            try {
                await this._eventStream.RequestStream.WriteAsync(evtBatch).ConfigureAwait(false);
            }
            catch (Exception ex) {
                _tcs.TrySetException(ex);
                return false;
            }
            return true;
        }
    }
}
