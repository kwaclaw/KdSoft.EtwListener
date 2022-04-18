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
        ) : base(sink, logger, batchSize, maxWriteDelayMSecs) {
            this._channel = Channel.CreateUnbounded<EtwEvent>(new UnboundedChannelOptions {
                AllowSynchronousContinuations = true,
                SingleReader = true,
                SingleWriter = false
            });
            this._etwEventPool = new DefaultObjectPool<EtwEvent>(new DefaultPooledObjectPolicy<EtwEvent>(), (int)batchSize);
            this._lastWrittenMSecs = Environment.TickCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool PostEvent(TraceEvent evt) {
            if (_stoppingTokenSource?.Token.IsCancellationRequested ?? true) {
                return false;
            }
            var etwEvent = _etwEventPool.Get();
            var posted = _channel.Writer.TryWrite(etwEvent.SetTraceEvent(evt));
            if (!posted)
                _logger.LogInformation("Could not post trace event {eventIndex}.", evt.EventIndex);
            return posted;
        }

        /// <summary>
        /// Timer callback that triggers periodical write operations even if the event batch is not full
        /// </summary>
        void TimerCallback(object? state) {
            if (_stoppingTokenSource?.Token.IsCancellationRequested ?? true) {
                return;
            }
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

            cts.Token.Register(() => {
                _channel.Writer.TryComplete();
                // we need to dispose the event sink, because WriteBatchAsync() would never return due to retry logic.
                base.DisposeAsync();  // this is not supposed to throw !
            });

            _timer = new Timer(TimerCallback);
            try {
                bool isCompleted;

                do {
                    isCompleted = true;
                    var batch = new EtwEventBatch();

                    try {
                        // when cts == null then we are just looping through the rest of the data
                        await foreach (var evt in _channel.Reader.ReadAllAsync(cts?.Token ?? default).ConfigureAwait(false)) {
                            // this does not get called when the loop returns without yielding data
                            isCompleted = false;

                            // _emptyEvent instance indicates we should write the batch even if incomplete
                            if (ReferenceEquals(_emptyEvent, evt)) {
                                if (batch.Events.Count == 0)
                                    continue;
                                break;
                            }
                            batch.Events.Add(evt);

                            // write the batch if it is full
                            if (batch.Events.Count >= _batchSize) {
                                break;
                            }
                        }

                        Volatile.Write(ref _lastWrittenMSecs, Environment.TickCount);
                        _logger.LogInformation("Received batch with {eventCount} events.", batch.Events.Count);

                        // this should not throw if event sink is implemented correctly
                        var success = await WriteBatchAsync(batch).ConfigureAwait(false);
                        if (!success) {
                            _channel.Writer.TryComplete();
                            isCompleted = true;
                            break;
                        }
                    }
                    // gets triggered when the _stoppingTokenSource gets cancelled, even before the registered callback
                    catch (OperationCanceledException) {
                        _channel.Writer.TryComplete();
                    }
                    catch (Exception ex) {
                        _logger.LogError(ex, "Error reading from event channel.");
                        throw;
                    }
                    finally {
                        if (_channel.Reader.Completion.IsCompleted) {
                            isCompleted = true;
                        }
                        foreach (var evt in batch.Events) {
                            evt.Payload.Clear();
                            _etwEventPool.Return(evt);
                        }
                    }

                } while (!isCompleted);
            }
            finally {
                // let's not wait for reader completion, because we might not be able to continue writing to the event sink
                // await _channel.Reader.Completion.ConfigureAwait(false);
                // already done in registered cancellation callback
                // await base.DisposeAsync().ConfigureAwait(false);
                var ctsToDispose = Interlocked.Exchange(ref _stoppingTokenSource, null);
                if (ctsToDispose != null) {
                    ctsToDispose.Dispose();
                }
                // may throw exception
                await _sink.RunTask.ConfigureAwait(false);
            }
        }

        public override async ValueTask DisposeAsync() {
            var cts = Interlocked.Exchange(ref _stoppingTokenSource, null);
            if (cts == null)  // already disposed
                return;
            try {
                cts.Cancel();
                var runTask = this.RunTask;
                if (runTask != null)
                    await runTask.ConfigureAwait(false);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error closing event channel.");
            }
            finally {
                cts.Dispose();
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
