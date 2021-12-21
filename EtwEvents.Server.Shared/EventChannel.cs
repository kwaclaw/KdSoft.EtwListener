using System;
using System.Threading;
using System.Threading.Tasks;
using KdSoft.EtwLogging;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Extensions.Logging;

namespace KdSoft.EtwEvents.Server
{
    public abstract class EventChannel
    {
        protected readonly IEventSink _sink;
        protected readonly ILogger _logger;

        protected int _batchSize;
        protected int _maxWriteDelayMSecs;
        protected Timer? _timer;

        protected EventChannel(
            IEventSink sink,
            ILogger logger,
            int batchSize,
            int maxWriteDelayMSecs
        ) {
            this._sink = sink;
            this._logger = logger;
            this._batchSize = batchSize;
            this._maxWriteDelayMSecs = maxWriteDelayMSecs;
        }

        // this should not throw if event sink is implemented correctly
        protected async ValueTask<bool> WriteBatchAsync(EtwEventBatch batch) {
            var result = await _sink.WriteAsync(batch).ConfigureAwait(false);
            if (!result) {
                // we assume that the IEventSink.RunTask is now complete and the event sink will be closed
                return result;
            }
            result = await _sink.FlushAsync().ConfigureAwait(false);
            return result;
        }

        protected abstract Task ProcessBatches(CancellationToken stoppingToken);

        public virtual ValueTask DisposeAsync() {
            _timer?.Dispose();
            return _sink.DisposeAsync();
        }

        /// <summary>
        /// Changes batch size.
        /// </summary>
        /// <param name="newBatchSize">New batch size.</param>
        /// <returns>Old batch size.</returns>
        public virtual int ChangeBatchSize(int newBatchSize) {
            return Interlocked.Exchange(ref _batchSize, newBatchSize);
        }

        /// <summary>
        /// Changes maximum write delay and updates timer.
        /// </summary>
        /// <param name="newWriteDelayMSecs">New write delay in milliseconds.</param>
        /// <returns>Old write delay.</returns>
        public virtual int ChangeWriteDelay(int newWriteDelayMSecs) {
            var oldDelay = Interlocked.Exchange(ref _maxWriteDelayMSecs, newWriteDelayMSecs);
            _timer?.Change(newWriteDelayMSecs, newWriteDelayMSecs);
            return oldDelay;
        }

        public abstract void PostEvent(TraceEvent evt);

        public Task? RunTask { get; private set; }

        public void StartProcessing(Action<Task> continuation, CancellationToken stoppingToken) {
            if (RunTask != null) {
                throw new InvalidOperationException("Already processing");
            }
            var runTask = ProcessBatches(stoppingToken);
            this.RunTask = runTask.ContinueWith(continuation);
        }
    }
}
