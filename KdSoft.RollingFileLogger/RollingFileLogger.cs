using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.ObjectPool;

namespace KdSoft.Logging {
    public class RollingFileLogger: ILogger {
        readonly ObjectPool<StringBuilder> _stringBuilderPool;
        readonly ArrayBufferWriter<byte> _bufferWriter;
        readonly RollingFileFactory _fileFactory;
        readonly string _category;
        readonly LogLevel _minLevel;
        readonly Channel<string> _channel;
        readonly int _batchSize;
        readonly int _maxWriteDelayMSecs;

        static readonly string _batchTerminator = string.Intern("##batch##");

        int _isDisposed = 0;
        int _lastWrittenMSecs;
        int _batchCounter;

        public Task<bool> RunTask { get; }

        public RollingFileLogger(RollingFileFactory fileFactory, string category, LogLevel minLevel, int batchSize, int maxWriteDelayMSecs) {
            this._fileFactory = fileFactory;
            this._category = category;
            this._minLevel = minLevel;
            this._batchSize = batchSize;
            this._maxWriteDelayMSecs = maxWriteDelayMSecs;

            this._channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions {
                AllowSynchronousContinuations = false,
                SingleReader = true,
                SingleWriter = false
            });

            var objectPoolProvider = new DefaultObjectPoolProvider();
            _stringBuilderPool = objectPoolProvider.CreateStringBuilderPool();

            _bufferWriter = new ArrayBufferWriter<byte>(1024);
            _lastWrittenMSecs = Environment.TickCount;

            RunTask = Process();
        }

        public bool IsDisposed {
            get {
                Interlocked.MemoryBarrier();
                var isDisposed = this._isDisposed;
                Interlocked.MemoryBarrier();
                return isDisposed > 0;
            }
        }

        // returns true if actually disposed, false if already disposed
        bool InternalDispose() {
            var oldDisposed = Interlocked.CompareExchange(ref _isDisposed, 99, 0);
            if (oldDisposed == 0) {
                _channel.Writer.TryComplete();
                return true;
            }
            return false;
        }

        public void Dispose() {
            if (InternalDispose()) {
                _channel.Reader.Completion.Wait();
                _fileFactory.Dispose();
            }
        }

        // Warning: ValueTasks should not be awaited multiple times
        public async ValueTask DisposeAsync() {
            if (InternalDispose()) {
                await _channel.Reader.Completion.ConfigureAwait(false);
                await _fileFactory.DisposeAsync().ConfigureAwait(false);
            }
        }

        public IDisposable BeginScope<TState>(TState state) => NullLogger.Instance.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && logLevel >= _minLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) {
            if (!IsEnabled(logLevel)) {
                return;
            }
            if (formatter == null) {
                throw new ArgumentNullException(nameof(formatter));
            }

            string message = formatter(state, exception);

            if (string.IsNullOrEmpty(message)) {
                return;
            }

            var sb = _stringBuilderPool.Get();
            try {
                sb.Clear();
                var timestamp = _fileFactory.UseLocalTime ? DateTimeOffset.Now : DateTimeOffset.UtcNow;
                sb.BuildEntryText(_category, logLevel, eventId, message, exception, null, timestamp);
                var written = _channel.Writer.TryWrite(sb.ToString());

                var itemCount = Interlocked.Increment(ref _batchCounter);
                // checking for exact match resolves issue with multiple concurrent increments,
                // only one of them will match and trigger the bacth terminator message
                if (itemCount == _batchSize) {
                    Volatile.Write(ref _batchCounter, 0);
                    Volatile.Write(ref _lastWrittenMSecs, Environment.TickCount);
                    _channel.Writer.TryWrite(_batchTerminator);
                }
            }
            finally {
                _stringBuilderPool.Return(sb);
            }
        }

        /// <summary>
        /// Timer callback that triggers periodical write operations even if the event batch is not full
        /// </summary>
        void TimerCallback(object? state) {
            try {
                var lastCheckedTicks = Interlocked.Exchange(ref _lastWrittenMSecs, Environment.TickCount);
                // integer subtraction is immune to rollover, e.g. unchecked(int.MaxValue + y) - (int.MaxValue - x) = y + x;
                // Environment.TickCount rolls over from int.Maxvalue to int.MinValue!
                var deltaTicks = Environment.TickCount - lastCheckedTicks;
                if (deltaTicks > _maxWriteDelayMSecs) {
                    Volatile.Write(ref _batchCounter, 0);
                    _channel.Writer.TryWrite(_batchTerminator);
                }
            }
            catch { }
        }

        async Task<bool> ProcessBatchToBuffer() {
            await foreach (var message in _channel.Reader.ReadAllAsync().ConfigureAwait(false)) {
                if (ReferenceEquals(message, _batchTerminator))
                    return false;
                Encoding.UTF8.GetBytes(message, _bufferWriter);
            }
            // batch is complete, we can write it now to the file
            return true;
        }

        async Task<bool> WriteBatchAsync(FileStream stream) {
            var messageBatch = _bufferWriter.WrittenMemory;
            if (messageBatch.IsEmpty)
                return true;

            await stream.WriteAsync(messageBatch).ConfigureAwait(false);
            _bufferWriter.Clear();

            await stream.FlushAsync().ConfigureAwait(false);
            return true;
        }

        async Task ProcessBatches() {
            bool isCompleted;
            do {
                // checks rollover conditions and returns appropriate file stream
                var stream = await _fileFactory.GetCurrentFileStream().ConfigureAwait(false);
                isCompleted = await ProcessBatchToBuffer().ConfigureAwait(false);
                await WriteBatchAsync(stream).ConfigureAwait(false);
            } while (!isCompleted);
        }

        public async Task<bool> Process() {
            Task processTask;
            using (var timer = new Timer(TimerCallback)) {
                processTask = ProcessBatches();
                timer.Change(_maxWriteDelayMSecs, _maxWriteDelayMSecs);
                await _channel.Reader.Completion.ConfigureAwait(false);
            }
            await processTask.ConfigureAwait(false);
            return IsDisposed;
        }
    }
}
