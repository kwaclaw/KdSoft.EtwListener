using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using KdSoft.EtwLogging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using tracing = Microsoft.Diagnostics.Tracing;

namespace KdSoft.EtwEvents.Server
{
    public class ChannelEventProcessor
    {
        readonly ObjectPool<EtwEvent> _etwEventPool;
        readonly Channel<EtwEvent> _channel;
        readonly ILogger _logger;

        int _batchSize;
        int _maxWriteDelayMSecs;
        int _lastWrittenMSecs;

        public ChannelEventProcessor(
            ILogger logger,
            int batchSize = 100,
            int maxWriteDelayMSecs = 400
        ) {
            this._logger = logger;
            this._batchSize = batchSize;
            this._maxWriteDelayMSecs = maxWriteDelayMSecs;

            this._channel = Channel.CreateUnbounded<EtwEvent>(new UnboundedChannelOptions {
                AllowSynchronousContinuations = true,
                SingleReader = true,
                SingleWriter = false
            });
            this._etwEventPool = new DefaultObjectPool<EtwEvent>(new DefaultPooledObjectPolicy<EtwEvent>(), batchSize);
            this._lastWrittenMSecs = Environment.TickCount;
        }

        protected virtual ValueTask<bool> WriteBatchAsync(EtwEventBatch batch) {
            return ValueTask.FromResult(true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void PostEvent(tracing.TraceEvent evt) {
            var etwEvent = _etwEventPool.Get();
            var posted = _channel.Writer.TryWrite(etwEvent.SetTraceEvent(evt));
            if (!posted)
                _logger.LogInformation("Could not post trace event {eventIndex}.", evt.EventIndex) ;
        }

        static readonly EtwEvent _emptyEvent = new();

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

        async Task ProcessBatches() {
            bool isCompleted;

            do {
                isCompleted = true;
                var batch = new EtwEventBatch();

                try {
                    await foreach (var evt in _channel.Reader.ReadAllAsync().ConfigureAwait(false)) {
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
                    await WriteBatchAsync(batch).ConfigureAwait(false);
                }
                finally {
                    foreach (var evt in batch.Events) {
                        _etwEventPool.Return(evt);
                    }
                }

            } while (!isCompleted);
        }

        public async Task Process(RealTimeTraceSession session, CancellationToken stoppingToken) {
            Task processTask;
            using (var timer = new Timer(TimerCallback)) {
                processTask = ProcessBatches();
                timer.Change(_maxWriteDelayMSecs, _maxWriteDelayMSecs);
                await session.StartEvents(PostEvent, stoppingToken).ConfigureAwait(false);
            }
            await _channel.Reader.Completion.ConfigureAwait(false);
            await processTask.ConfigureAwait(false);
        }
    }
}
