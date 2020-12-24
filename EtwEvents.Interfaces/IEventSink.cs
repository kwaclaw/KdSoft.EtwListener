﻿using System;
using System.Threading.Tasks;
using KdSoft.EtwLogging;

namespace KdSoft.EtwEvents.Client.Shared
{
    public interface IEventSink: IAsyncDisposable, IDisposable, IEquatable<IEventSink>
    {
        /// <summary>
        /// Identifier for event sink.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Task that completes when <see cref="IEventSink"/> is finished/closed.
        /// </summary>
        /// <returns><c>true</c> when already disposed, <c>false</c> otherwise.</returns>
        Task<bool> RunTask { get; }

        /// <summary>
        /// Writes event asynchronously. This may queue event for batched writing and may return synchronously.
        /// Must not be called concurrently with itself or <see cref="FlushAsync"/>.
        /// Must not throw exception before <see cref="ValueTask"/> is returned.
        /// </summary>
        /// <param name="evt">Event to write.</param>
        /// <param name="sequenceNo">Incrementing sequence number for event.</param>
        /// <returns><c>true</c> if writing was successful (and can continue), <c>false</c> otherwise.</returns>
        ValueTask<bool> WriteAsync(EtwEvent evt, long sequenceNo);

        /// <summary>
        /// Flushes queue, performs pending writes.
        /// Must not be called concurrently with itself or <see cref="WriteAsync(EtwEvent, long)"/>.
        /// Must not throw exception before <see cref="ValueTask"/> is returned.
        /// </summary>
        /// <returns><c>true</c> if flushing was successful (and can continue), <c>false</c> otherwise.</returns>
        ValueTask<bool> FlushAsync();
    }
}