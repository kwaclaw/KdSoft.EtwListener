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
    public class ChannelEventProcessor: EventProcessor
    {
        readonly WriteBatchAsync _writeBatchAsync;
        readonly ObjectPool<EtwEvent> _etwEventPool;
        readonly Channel<EtwEvent> _channel;
        readonly ILogger _logger;
        readonly int _batchSize;

        int _lastWrittenMSecs;
        int _maxWriteDelayMSecs;

        public ChannelEventProcessor(
            WriteBatchAsync writeBatchAsync,
            ILogger logger,
            int batchSize = 100
        ) {
            this._writeBatchAsync = writeBatchAsync;
            this._logger = logger;
            this._channel = Channel.CreateUnbounded<EtwEvent>(new UnboundedChannelOptions {
                AllowSynchronousContinuations = true,
                SingleReader = true,
                SingleWriter = false
            });
            this._batchSize = batchSize;
            this._etwEventPool = new DefaultObjectPool<EtwEvent>(new DefaultPooledObjectPolicy<EtwEvent>(), batchSize);
            this._lastWrittenMSecs = Environment.TickCount;
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
                    await _writeBatchAsync(batch).ConfigureAwait(false);
                }
                finally {
                    foreach (var evt in batch.Events) {
                        _etwEventPool.Return(evt);
                    }
                }

            } while (!isCompleted);
        }

        public override async Task Process(RealTimeTraceSession session, TimeSpan maxWriteDelay, CancellationToken stoppingToken) {
            this._maxWriteDelayMSecs = (int)maxWriteDelay.TotalMilliseconds;
            Task processTask;
            using (var timer = new Timer(TimerCallback)) {
                processTask = ProcessBatches();
                timer.Change(maxWriteDelay, maxWriteDelay);
                await session.StartEvents(PostEvent, stoppingToken).ConfigureAwait(false);
            }
            await _channel.Reader.Completion.ConfigureAwait(false);
            await processTask.ConfigureAwait(false);
        }
    }
}
