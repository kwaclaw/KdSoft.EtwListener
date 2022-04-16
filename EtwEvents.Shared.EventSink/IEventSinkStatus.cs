using System;
using KdSoft.EtwLogging;

namespace KdSoft.EtwEvents
{
    /// <summary>
    /// Describes status of an <see cref="IEventSink">event sink</see>.
    /// </summary>
    public interface IEventSinkStatus
    {
        /// <summary>
        /// Number of retries since last successful <see cref="IEventSink.WriteAsync(EtwEventBatch)"/>.
        /// </summary>
        int NumRetries { get; }

        /// <summary>
        /// Time of first retry since last successful <see cref="IEventSink.WriteAsync(EtwEventBatch)"/>, if applicable.
        /// </summary>
        DateTimeOffset RetryStartTime { get; }

        /// <summary>
        /// Last exception thrown by <see cref="IEventSink.WriteAsync(EtwEventBatch)"/>.
        /// </summary>
        Exception? LastError { get; }
    }
}
