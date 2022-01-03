using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using KdSoft.EtwLogging;
using KdSoft.Faster;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using tracing = Microsoft.Diagnostics.Tracing;

namespace KdSoft.EtwEvents.Server
{
    public class PersistentEventChannel: EventChannel
    {
        readonly ObjectPool<EtwEvent> _etwEventPool;
        readonly ArrayBufferWriter<byte> _bufferWriter;
        readonly FasterChannel _channel;

        // need a non-empty sentinel message, as FasterChannel ignores empty messages;
        // this messages must also never match a regular message written to the channel
        static readonly ReadOnlyMemory<byte> _batchSentinel = new byte[4] { 0, 0, 0, 0 };

        int _lastWrittenMSecs;
        uint _batchCounter;
        CancellationTokenSource? _stoppingTokenSource;

        PersistentEventChannel(
            IEventSink sink,
            ILogger logger,
            string filePath,
            uint batchSize = 100,
            uint maxWriteDelayMSecs = 400
        ) : base(sink, logger, batchSize, maxWriteDelayMSecs) {
            this._channel = new FasterChannel(filePath);
            this._etwEventPool = new DefaultObjectPool<EtwEvent>(new DefaultPooledObjectPolicy<EtwEvent>(), (int)batchSize);
            this._bufferWriter = new ArrayBufferWriter<byte>(1024);
            this._lastWrittenMSecs = Environment.TickCount;
        }

        public override void PostEvent(tracing.TraceEvent evt) {
            if (_stoppingTokenSource?.Token.IsCancellationRequested ?? false) {
                return;
            }

            var etwEvent = _etwEventPool.Get();
            try {
                etwEvent.SetTraceEvent(evt);
                _bufferWriter.Clear();
                etwEvent.WriteTo(_bufferWriter);
            }
            finally {
                etwEvent.Payload.Clear();
                _etwEventPool.Return(etwEvent);
            }

            var posted = _channel.TryWrite(_bufferWriter.WrittenMemory);
            if (posted) {
                var batchCount = Interlocked.Increment(ref _batchCounter);
                // checking for exact match resolves issue with multiple concurrent increments,
                // only one of them will match and trigger the bacth terminator message
                if (batchCount == _batchSize) {
                    Volatile.Write(ref _batchCounter, 0);
                    Volatile.Write(ref _lastWrittenMSecs, Environment.TickCount);
                    // need a non-empty sentinel message, as FasterChannel ignores empty messages
                    _channel.TryWrite(_batchSentinel);
                    // this makes reader move forward, as the reader is only driven by committed messages
                    _channel.Commit(true);
                }
            }
            else {
                _logger.LogInformation("Could not post trace event {eventIndex}.", evt.EventIndex);
            }
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
                Volatile.Write(ref _batchCounter, 0);
                // need a non-empty sentinel message, as FasterChannel ignores empty messages;
                if (_channel.TryWrite(_batchSentinel))
                    _channel.Commit(true);
                else
                    _logger.LogInformation($"Could not post batch sentinel.");

            }
        }

        protected override async Task ProcessBatches(CancellationToken stoppingToken) {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var oldCts = Interlocked.CompareExchange(ref _stoppingTokenSource, cts, null);
            if (oldCts != null) {
                cts.Cancel();
                cts.Dispose();
                throw new InvalidOperationException("Channel already stopped.");
            }

            await Task.Yield();

            using var reader = _channel.GetNewReader();

            using (_timer = new Timer(TimerCallback)) {

                do {
                    var batch = new EtwEventBatch();

                    //TODO this does not update the _nextAddress field in reader!, but it works without NullReferenceExceptions in TryEnqueue
                    //await foreach (var (owner, length, _, _) in reader.GetAsyncEnumerable(stoppingToken).ConfigureAwait(false)) {
                    await foreach (var (owner, length) in reader.ReadAllAsync(cts.Token).ConfigureAwait(false)) {
                        using (owner) {
                            var byteSequence = new ReadOnlySequence<byte>(owner.Memory).Slice(0, length);

                            // check if this data items indicates the end of a batch
                            if (length == _batchSentinel.Length && byteSequence.FirstSpan.SequenceEqual(_batchSentinel.Span)) {
                                if (batch.Events.Count == 0)
                                    continue;
                                break;
                            }

                            var etwEvent = EtwEvent.Parser.ParseFrom(byteSequence);
                            batch.Events.Add(etwEvent);
                        }
                    }

                    if (batch.Events.Count > 0) {
                        _logger.LogInformation("Received batch with {eventCount} events.", batch.Events.Count);
                        bool success = await WriteBatchAsync(batch).ConfigureAwait(false);
                        if (success) {
                            reader.Truncate();
                            await _channel.CommitAsync(default).ConfigureAwait(false);
                        }
                        else { // event sink failed or closed
                            break;
                        }
                    }

                } while (!cts.Token.IsCancellationRequested);

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
                var runTask = this.RunTask;
                if (runTask != null)
                    await runTask.ConfigureAwait(false);
                _channel.Dispose();
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error closing event channel.");
            }
            finally {
                await base.DisposeAsync().ConfigureAwait(false);
            }
        }

        public static PersistentEventChannel Create(
            IEventSink sink,
            ILogger logger,
            string filePath,
            uint batchSize = 100,
            uint maxWriteDelayMSecs = 400
        ) {
            var result = new PersistentEventChannel(sink, logger, filePath, batchSize, maxWriteDelayMSecs);
            return result;
        }
    }
}
