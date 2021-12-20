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
        int _batchCounter;

        PersistentEventChannel(
            IEventSink sink,
            ILogger logger,
            int batchSize,
            int maxWriteDelayMSecs,
            FasterChannel channel,
            ObjectPool<EtwEvent> etwEventPool,
            ArrayBufferWriter<byte> bufferWriter
        ) : base(sink, logger, batchSize, maxWriteDelayMSecs) {
            this._channel = channel;
            this._etwEventPool = etwEventPool;
            this._bufferWriter = bufferWriter;
            this._lastWrittenMSecs = Environment.TickCount;
        }

        PersistentEventChannel(
            IEventSink sink,
            ILogger logger,
            string filePath,
            int batchSize = 100,
            int maxWriteDelayMSecs = 400
        ) : base(sink, logger, batchSize, maxWriteDelayMSecs) {
            this._channel = new FasterChannel(filePath);
            this._etwEventPool = new DefaultObjectPool<EtwEvent>(new DefaultPooledObjectPolicy<EtwEvent>(), batchSize);
            this._bufferWriter = new ArrayBufferWriter<byte>(1024);
            this._lastWrittenMSecs = Environment.TickCount;
        }

        public override void PostEvent(tracing.TraceEvent evt) {
            var etwEvent = _etwEventPool.Get();
            try {
                etwEvent.SetTraceEvent(evt);

                _bufferWriter.Clear();
                etwEvent.WriteTo(_bufferWriter);
            }
            finally {
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
            await Task.Yield();

            using var reader = _channel.GetNewReader();

            using (_timer = new Timer(TimerCallback)) {

                do {
                    var batch = new EtwEventBatch();

                    //TODO this does not update the _nextAddress field in reader!, but it works without NullReferenceExceptions in TryEnqueue
                    //await foreach (var (owner, length, _, _) in reader.GetAsyncEnumerable(stoppingToken).ConfigureAwait(false)) {
                    await foreach (var (owner, length) in reader.ReadAllAsync(stoppingToken).ConfigureAwait(false)) {
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

                } while (!stoppingToken.IsCancellationRequested);

            }
            _timer = null;

            await _sink.RunTask.ConfigureAwait(false);
        }

        Task _runTask;
        public override Task RunTask => _runTask;

        public override async ValueTask DisposeAsync() {
            try {
                _channel.Dispose();
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
            var result = new PersistentEventChannel(
                sink,
                this._logger,
                batchSize ?? this._batchSize,
                maxWriteDelayMSecs ?? this._maxWriteDelayMSecs,
                this._channel,
                this._etwEventPool,
                this._bufferWriter
            );
            result._runTask = result.ProcessBatches(stoppingToken);
            return result;
        }

        public static PersistentEventChannel Start(
            IEventSink sink,
            ILogger logger,
            CancellationToken stoppingToken,
            string filePath,
            int batchSize = 100,
            int maxWriteDelayMSecs = 400
        ) {
            var result = new PersistentEventChannel(sink, logger, filePath, batchSize, maxWriteDelayMSecs);
            result._runTask = result.ProcessBatches(stoppingToken);
            return result;
        }
    }
}
