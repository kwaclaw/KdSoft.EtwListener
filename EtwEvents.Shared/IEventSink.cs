using System;
using System.Threading.Tasks;
using KdSoft.EtwLogging;

namespace KdSoft.EtwEvents
{
    /// <summary>
    /// An event sink consumes ETW events.
    /// An event sink does not swallow exceptions, it is the application's responsibility to react to an error.
    /// To close/stop an event sink means to dispose it. Multiple DisposeAsync() calls must not throw an exception.
    /// </summary>
    public interface IEventSink: IAsyncDisposable  //, IEquatable<IEventSink>
    {
        /// <summary>
        /// Task that completes when <see cref="IEventSink"/> is finished/closed or has failed.
        /// The result indicates if the event sink was disposed (<c>true</c>) or still needs to be disposed (<c>false</c>).
        /// When the Task throws an exception then we can assume that the event sink was not disposed.
        /// </summary>
        Task<bool> RunTask { get; }

        /// <summary>
        /// Writes event asynchronously, but may execute synchronously. The event may be queued for batched writing.
        /// Must not be called concurrently with itself, <see cref="FlushAsync"/> or <see cref="IAsyncDisposable.DisposeAsync"/>.
        /// Must not throw an exception, exceptions in the implementation must be handled and forwarded to RunTask, to be thrown there.
        /// </summary>
        /// <param name="evt">Event to write.</param>
        /// <returns><c>true</c> if writing was successful (and can continue), <c>false</c> otherwise.</returns>
        ValueTask<bool> WriteAsync(EtwEvent evt);

        /// <summary>
        /// Writes batch of events asynchronously, but may execute synchronously. The events may be queued for batched writing.
        /// Must not be called concurrently with itself, <see cref="FlushAsync"/> or <see cref="IAsyncDisposable.DisposeAsync"/>.
        /// Must not throw an exception, exceptions in the implementation must be handled and forwarded to RunTask, to be thrown there.
        /// </summary>
        /// <param name="evts">Events to write.</param>
        /// <returns><c>true</c> if writing was successful (and can continue), <c>false</c> otherwise.</returns>
        ValueTask<bool> WriteAsync(EtwEventBatch evtBatch);

        /// <summary>
        /// Flushes queue, performs pending writes. Must be called to ensure events have been written, even if the event sink has no buffer.
        /// Must not be called concurrently with itself, <see cref="WriteAsync"/> or <see cref="IAsyncDisposable.DisposeAsync"/>.
        /// Must not throw an exception, exceptions in the implementation must be handled and forwarded to RunTask, to be thrown there.
        /// </summary>
        /// <returns><c>true</c> if flushing was successful (and can continue), <c>false</c> otherwise.</returns>
        ValueTask<bool> FlushAsync();
    }
}
