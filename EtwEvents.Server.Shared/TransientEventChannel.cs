using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using KdSoft.EtwLogging;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace KdSoft.EtwEvents.Server
{
    public class TransientEventChannel: EventChannel
    {
        readonly ObjectPool<EtwEvent> _etwEventPool;
        readonly Channel<EtwEvent> _channel;

        static readonly EtwEvent _emptyEvent = new();

        int _lastWrittenMSecs;

        TransientEventChannel(
            IEventSink sink,
            ILogger logger,
            int batchSize,
            int maxWriteDelayMSecs,
            Channel<EtwEvent> channel,
            ObjectPool<EtwEvent> etwEventPool
        ): base(sink, logger, batchSize, maxWriteDelayMSecs) {
            this._channel = channel;
            this._etwEventPool = etwEventPool;
            this._lastWrittenMSecs = Environment.TickCount;
        }

        TransientEventChannel(
            IEventSink sink,
            ILogger logger,
            int batchSize = 100,
            int maxWriteDelayMSecs = 400
        ): base(sink, logger, batchSize, maxWriteDelayMSecs) {
            this._channel = Channel.CreateUnbounded<EtwEvent>(new UnboundedChannelOptions {
                AllowSynchronousContinuations = true,
                SingleReader = true,
                SingleWriter = false
            });
            this._etwEventPool = new DefaultObjectPool<EtwEvent>(new DefaultPooledObjectPolicy<EtwEvent>(), batchSize);
            this._lastWrittenMSecs = Environment.TickCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void PostEvent(TraceEvent evt) {
            var etwEvent = _etwEventPool.Get();
            var posted = _channel.Writer.TryWrite(etwEvent.SetTraceEvent(evt));
            if (!posted)
                _logger.LogInformation("Could not post trace event {eventIndex}.", evt.EventIndex);
        }

        /// <summary>
        /// Timer callback that triggers periodical write operations even if the event batch is not full
        /// </summary>
        void TimerCallback(object? state) {
            var lastCheckedTicks = Interlocked.Exchange(ref _lastWrittenMSecs, Environment.TickCount);
            // integer subtraction is immune to rollover, e.g. unchecked(int.MaxValue + y) - (int.MaxValue - x) = y + x;
            // Environment.TickCount rolls over from int.Maxvalue to int.MinValue!
            var deltaTicks = Environment.TickCount - lastCheckedTicks;
            if (deltaTicks > _maxWriteDelayMSecs) {
                _channel.Writer.TryWrite(_emptyEvent);
            }
        }

        /// <summary>
        /// Processes events written to the channel.
        /// </summary>
        /// <param name="stoppingToken">CancellationToken to use for stopping the channel.</param>
        protected override async Task ProcessBatches(CancellationToken stoppingToken) {
            bool isCompleted;

            using (_timer = new Timer(TimerCallback)) {

                do {
                    isCompleted = true;
                    var batch = new EtwEventBatch();

                    try {
                        await foreach (var evt in _channel.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false)) {
                            // _emptyEvent instance indicates we should write the batch even if incomplete
                            if (ReferenceEquals(_emptyEvent, evt)) {
                                if (batch.Events.Count == 0)
                                    continue;
                                isCompleted = false;
                                break;
                            }
                            batch.Events.Add(evt);
                            // write the batch if it is full
                            if (batch.Events.Count >= _batchSize) {
                                isCompleted = false;
                                break;
                            }
                        }

                        Volatile.Write(ref _lastWrittenMSecs, Environment.TickCount);
                        _logger.LogInformation("Received batch with {eventCount} events.", batch.Events.Count);

                        // this should not throw if event sink is implemented correctly
                        var success = await WriteBatchAsync(batch).ConfigureAwait(false);
                        isCompleted = !success;
                    }
                    finally {
                        foreach (var evt in batch.Events) {
                            _etwEventPool.Return(evt);
                        }
                    }

                } while (!isCompleted && !stoppingToken.IsCancellationRequested);

            }
            _timer = null;

            await _sink.RunTask.ConfigureAwait(false);
        }

        Task _runTask;
        public override Task RunTask => _runTask;

        public override async ValueTask DisposeAsync() {
            try {
                _channel.Writer.TryComplete();
                await _channel.Reader.Completion.ConfigureAwait(false);
                await base.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error closing event channel.");
            }
        }

        public override EventChannel Clone(
            IEventSink sink,
            CancellationToken stoppingToken,
            int? batchSize = null,
            int? maxWriteDelayMSecs = null
        ) {
            var result = new TransientEventChannel(
                sink,
                this._logger,
                batchSize ?? this._batchSize,
                maxWriteDelayMSecs ?? this._maxWriteDelayMSecs,
                this._channel,
                this._etwEventPool
            );
            result._runTask = result.ProcessBatches(stoppingToken);
            return result;
        }

        public static TransientEventChannel Start(
            IEventSink sink,
            ILogger logger,
            CancellationToken stoppingToken,
            int batchSize = 100,
            int maxWriteDelayMSecs = 400
        ) {
            var result = new TransientEventChannel(sink, logger, batchSize, maxWriteDelayMSecs);
            result._runTask = result.ProcessBatches(stoppingToken);
            return result;
        }
    }
}
