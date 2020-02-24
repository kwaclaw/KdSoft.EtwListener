using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using KdSoft.EtwLogging;

namespace EtwEvents.WebClient
{
    public interface IEventSink: IAsyncDisposable, IDisposable, IEquatable<IEventSink>
    {
        /// <summary>
        /// Identifier for event sink.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Initializes sink instance for writing. 
        /// </summary>
        /// <param name="cancelToken">Cancellation token to stop ongoing operations.</param>
        //void Initialize(CancellationToken cancelToken);

        /// <summary>
        /// Writes event asynchronously. This may queue event for batched writing and may return synchronously.
        /// Must not be called concurrently with <see cref="FlushAsync"/>.
        /// </summary>
        /// <param name="evt">Event to write.</param>
        /// <param name="sequenceNo">Incrementing sequence number for event.</param>
        /// <returns><c>true</c> if writing was successful (and can continue), <c>false</c> otherwise.</returns>
        ValueTask<bool> WriteAsync(EtwEvent evt, long sequenceNo);

        /// <summary>
        /// Flushes queue, performs pending writes.
        /// Must not be called concurrently with <see cref="WriteAsync(EtwEvent, long)"/>.
        /// </summary>
        /// <returns><c>true</c> if flushing was successful (and can continue), <c>false</c> otherwise.</returns>
        ValueTask<bool> FlushAsync();
    }

    public class EventSinkComparer: IEqualityComparer<IEventSink>
    {
        public static EventSinkComparer Default { get; } = new EventSinkComparer();

        public bool Equals([AllowNull] IEventSink x, [AllowNull] IEventSink y) {
            if (object.ReferenceEquals(x, y))
                return true;
            if (x == null || y == null)
                return false;
            return StringComparer.Ordinal.Equals(x.Id, y.Id);
        }

        public int GetHashCode([DisallowNull] IEventSink obj) {
            return obj?.Id?.GetHashCode(StringComparison.Ordinal) ?? 0;
        }
    }
}
