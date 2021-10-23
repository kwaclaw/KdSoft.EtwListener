﻿using System;
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
    public class PersistentEventProcessor: IDisposable
    {
        readonly WriteBatchAsync _writeBatchAsync;
        readonly ObjectPool<EtwEvent> _etwEventPool;
        readonly ArrayBufferWriter<byte> _bufferWriter;
        readonly FasterChannel _channel;
        readonly ILogger _logger;
        readonly int _batchSize;

        // need a non-empty sentinel message, as FasterChannel ignores empty messages;
        // this messages must also never match a regular message written to the channel
        static readonly ReadOnlyMemory<byte> _batchSentinel = new byte[4] { 0, 0, 0, 0 };

        int _lastWrittenMSecs;
        int _maxWriteDelayMSecs;
        int _batchCounter;

        public PersistentEventProcessor(
            WriteBatchAsync writeBatchAsync,
            string filePath,
            ILogger logger,
            int batchSize = 100
        ) {
            this._writeBatchAsync = writeBatchAsync;
            this._logger = logger;
            this._batchSize = batchSize;
            this._channel = new FasterChannel(filePath);
            this._etwEventPool = new DefaultObjectPool<EtwEvent>(new DefaultPooledObjectPolicy<EtwEvent>(), batchSize);
            this._bufferWriter = new ArrayBufferWriter<byte>(1024);
            this._lastWrittenMSecs = Environment.TickCount;
        }

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
                    // need a non-empty sentinel message, as FasterChannel ignores empty messages
                    _channel.TryWrite(_batchSentinel);
                    // this makes reader move forward, as the reader is only driven by committed messages
                    _channel.Commit(true);
                }
            }
            else {
                _logger.LogInformation($"Could not post trace event {evt.EventIndex}.");
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

        async Task ProcessBatches(CancellationToken stoppingToken) {
            await Task.Yield();

            using (var reader = _channel.GetNewReader()) {
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
                        _logger.LogInformation($"Received batch with {batch.Events.Count} events.");
                        bool success = await _writeBatchAsync(batch).ConfigureAwait(false);
                        if (success) {
                            reader.Truncate();
                            await _channel.CommitAsync().ConfigureAwait(false);
                        }
                    }

                } while (!stoppingToken.IsCancellationRequested);
            }
        }

        public async Task Process(RealTimeTraceSession session, TimeSpan maxWriteDelay, CancellationToken stoppingToken) {
            this._maxWriteDelayMSecs = (int)maxWriteDelay.TotalMilliseconds;
            Task processTask;
            using (var timer = new Timer(TimerCallback)) {
                //var creationOptions = TaskCreationOptions.LongRunning | TaskCreationOptions.RunContinuationsAsynchronously;
                //processTask = Task.Factory.StartNew(() => ProcessBatches(stoppingToken), creationOptions).Unwrap();
                processTask = ProcessBatches(stoppingToken);
                timer.Change(maxWriteDelay, maxWriteDelay);
                await session.StartEvents(PostEvent, stoppingToken).ConfigureAwait(false);
            }
            await processTask.ConfigureAwait(false);
        }

        public void Dispose() {
            try { _channel.Dispose(); }
            catch { }
        }
    }
}
