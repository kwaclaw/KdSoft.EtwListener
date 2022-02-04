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
        /// Writes batch of events asynchronously, but may execute synchronously.
        /// The events should not be queued for processing in a subsequent call.
        /// Must not be called concurrently with itself.
        /// Must not throw an exception, exceptions must be handled and forwarded to RunTask, to be thrown there.
        /// </summary>
        /// <param name="evts">Events to write.</param>
        /// <returns><c>true</c> if writing was successful (and can continue), <c>false</c> otherwise.</returns>
        ValueTask<bool> WriteAsync(EtwEventBatch evtBatch);
    }
}
