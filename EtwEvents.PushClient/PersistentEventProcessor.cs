using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using KdSoft.EtwEvents.Client.Shared;
using KdSoft.EtwEvents.Server;
using KdSoft.EtwLogging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using tracing = Microsoft.Diagnostics.Tracing;

namespace KdSoft.EtwEvents.PushClient {
    public class PersistentEventProcessor: IDisposable
    {
        readonly IEventSink _sink;
        readonly ObjectPool<EtwEvent> _etwEventPool;
        readonly ArrayBufferWriter<byte> _bufferWriter;
        readonly FasterChannel _channel;
        readonly ILogger _logger;
        readonly int _batchSize;

        int _lastWrittenMSecs;
        int _maxWriteDelayMSecs;
        int _batchCounter;

        public PersistentEventProcessor(
            IEventSink sink,
            CancellationToken stoppingToken,
            ILogger logger,
            int batchSize = 100
        ) {
            this._sink = sink;
            this._logger = logger;
            this._batchSize = batchSize;
            this._channel = new FasterChannel();
            this._etwEventPool = new DefaultObjectPool<EtwEvent>(new DefaultPooledObjectPolicy<EtwEvent>(), batchSize);
            this._bufferWriter = new ArrayBufferWriter<byte>(1024);
            this._lastWrittenMSecs = Environment.TickCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void PostEvent(tracing.TraceEvent evt) {
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
                    _channel.TryWrite(_emptyBytes);
                    // this makes reader move forward, as the reader is only driven by committed messages
                    _channel.Commit();
                }
            }
            else {
                _logger.LogInformation($"Could not post trace event {evt.EventIndex}.");
            }
        }

        static readonly ReadOnlyMemory<byte> _emptyBytes = new byte[0];

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
                _channel.TryWrite(_emptyBytes);
            }
        }

        async Task ProcessBatches(CancellationToken stoppingToken) {
            bool isCompleted;
            long sequenceNo = 0;

            using (var reader = _channel.GetNewReader()) {
                do {
                    isCompleted = true;
                    var batch = new EtwEventBatch();

                    await foreach (var (owner, length) in reader.ReadAllAsync(stoppingToken).ConfigureAwait(false)) {
                        using (owner) {
                            // empty record indicates we should write the batch even if incomplete
                            if (length == 0) {
                                if (batch.Events.Count == 0)
                                    continue;
                                isCompleted = false;
                                break;
                            }

                            var byteSequence = new ReadOnlySequence<byte>(owner.Memory);
                            var etwEvent = EtwEvent.Parser.ParseFrom(byteSequence.Slice(0, length));
                            batch.Events.Add(etwEvent);
                        }
                    }

                    _logger.LogInformation($"Received batch with {batch.Events.Count} events.");

                    bool success = await _sink.WriteAsync(batch, sequenceNo).ConfigureAwait(false);
                    if (success) {
                        sequenceNo += batch.Events.Count;
                        reader.Truncate();
                        await _channel.CommitAsync().ConfigureAwait(false);
                    }

                } while (!isCompleted);
            }
        }

        public async Task Process(RealTimeTraceSession session, TimeSpan maxWriteDelay, CancellationToken stoppingToken) {
            this._maxWriteDelayMSecs = (int)maxWriteDelay.TotalMilliseconds;
            Task processTask;
            using (var timer = new Timer(TimerCallback)) {
                processTask = ProcessBatches(stoppingToken);
                timer.Change(maxWriteDelay, maxWriteDelay);
                await session.StartEvents(PostEvent, stoppingToken).ConfigureAwait(false);
            }
            //await _channel.Reader.Completion.ConfigureAwait(false);
            await processTask.ConfigureAwait(false);
        }

        public void Dispose() {
            try { _channel.Dispose(); }
            catch { }
        }
    }
}
