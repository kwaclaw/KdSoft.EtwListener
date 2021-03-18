using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using FASTER.core;

namespace KdSoft.EtwEvents.PushClient
{
    public class FasterChannel: IDisposable
    {
        readonly FasterLog _log;
        readonly IDevice _device;
        long _logicalAddress;

        public FasterChannel() {
            _device = Devices.CreateLogDevice(@"C:\Temp\logs\hlog.log");
            _log = new FasterLog(new FasterLogSettings { LogDevice = _device });
        }

        public bool TryWrite(ReadOnlyMemory<byte> item) {
            return _log.TryEnqueue(item.Span, out _logicalAddress);
        }

        public async ValueTask WriteAsync(ReadOnlyMemory<byte> item, CancellationToken cancellationToken = default) {
            _logicalAddress = await _log.EnqueueAsync(item, cancellationToken);
        }

        public FasterReader GetNewReader() {
            return new FasterReader(_log);
        }

        public void Commit(bool spinWait = false) {
            _log.Commit(spinWait);
        }

        public void Close() {
            _log.Dispose();
            _device.Dispose();
        }

        public ValueTask CommitAsync(CancellationToken cancellationToken = default) {
            return _log.CommitAsync(cancellationToken);
        }

        public void Dispose() {
            _log.Dispose();
            _device.Dispose();
        }
    }

    public class FasterReader: IDisposable {
        readonly FasterLog _log;
        readonly FasterLogScanIterator _iter;
        readonly MemoryPool<byte> _pool;

        long _nextAddress;

        public FasterReader(FasterLog log) {
            this._log = log;
            _nextAddress = log.BeginAddress;
            _iter = log.Scan(0, long.MaxValue, "channel");
            _pool = MemoryPool<byte>.Shared;
        }

        public bool TryRead([MaybeNullWhen(false)] out IMemoryOwner<byte> item, out int entryLength) {
            if (_iter.GetNext(_pool, out item, out entryLength, out var currentAddress, out _nextAddress)) {
                return true;
            }
            item = null;
            return false;
        }

        public async ValueTask<(IMemoryOwner<byte>, int)> ReadAsync(CancellationToken cancellationToken = default) {
            while (true) {
                if (!await WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                    throw new ChannelClosedException();

                if (TryRead(out IMemoryOwner<byte>? item, out int entryLength))
                    return (item, entryLength);
            }
        }

        public async IAsyncEnumerable<(IMemoryOwner<byte>, int)> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default) {
            await foreach ((IMemoryOwner<byte> entry, var entryLength, var currentAddress, var nextAddress) in _iter.GetAsyncEnumerable(_pool, cancellationToken)) {
                _nextAddress = nextAddress;
                yield return (entry, entryLength);

                // MoveNextAsync() would hang at TailAddress, waiting for more entries (that we don't add).
                // Note: If this happens and the test has to be canceled, there may be a leftover blob from the log.Commit(), because
                // the log device isn't Dispose()d; the symptom is currently a numeric string format error in DefaultCheckpointNamingScheme.
                if (nextAddress == _log.TailAddress)
                    break;
            }
        }

        public ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default) {
            return _iter.WaitAsync(cancellationToken);
        }

        public void Truncate() {
            _log.TruncateUntil(_nextAddress);
        }

        public void Dispose() {
            _iter.Dispose();
            _pool.Dispose();
        }
    }
}
