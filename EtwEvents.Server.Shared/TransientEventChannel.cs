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
        CancellationTokenSource? _stoppingTokenSource;

        TransientEventChannel(
            IEventSink sink,
            ILogger logger,
            uint batchSize = 100,
            uint maxWriteDelayMSecs = 400
        ): base(sink, logger, batchSize, maxWriteDelayMSecs) {
            this._channel = Channel.CreateUnbounded<EtwEvent>(new UnboundedChannelOptions {
                AllowSynchronousContinuations = true,
                SingleReader = true,
                SingleWriter = false
            });
            this._etwEventPool = new DefaultObjectPool<EtwEvent>(new DefaultPooledObjectPolicy<EtwEvent>(), (int)batchSize);
            this._lastWrittenMSecs = Environment.TickCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void PostEvent(TraceEvent evt) {
            if (_stoppingTokenSource?.Token.IsCancellationRequested ?? false) {
                _channel.Writer.TryComplete();
            }
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
            var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var oldCts = Interlocked.CompareExchange(ref _stoppingTokenSource, cts, null);
            if (oldCts != null) {
                cts.Cancel();
                cts.Dispose();
                throw new InvalidOperationException("Channel already stopped.");
            }

            bool isCompleted;

            using (_timer = new Timer(TimerCallback)) {

                do {
                    isCompleted = true;
                    var batch = new EtwEventBatch();

                    try {
                        await foreach (var evt in _channel.Reader.ReadAllAsync(cts.Token).ConfigureAwait(false)) {
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

                } while (!isCompleted && !cts.Token.IsCancellationRequested);

            }
            _timer = null;

            await _sink.RunTask.ConfigureAwait(false);
        }

        public override async ValueTask DisposeAsync() {
            try {
                var cts = _stoppingTokenSource;
                if (cts != null) {
                    cts.Cancel();
                    cts.Dispose();
                }
                await _channel.Reader.Completion.ConfigureAwait(false);
                var runTask = this.RunTask;
                if (runTask != null)
                    await runTask.ConfigureAwait(false);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error closing event channel.");
            }
            finally {
                await base.DisposeAsync().ConfigureAwait(false);
            }
        }

        public static TransientEventChannel Create(
            IEventSink sink,
            ILogger logger,
            uint batchSize = 100,
            uint maxWriteDelayMSecs = 400
        ) {
            var result = new TransientEventChannel(sink, logger, batchSize, maxWriteDelayMSecs);
            return result;
        }
    }
}
