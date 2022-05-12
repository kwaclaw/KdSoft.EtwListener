using System;

namespace KdSoft.EtwEvents
{
    /// <summary>
    /// Provides an event to be notified of status changes.
    /// </summary>
    public interface IEventSinkStatus
    {
        /// <summary>
        /// Triggered when the event sink's status changes.
        /// </summary>
        event Action Changed;
    }

    /// <summary>
    /// Gives access to the event sink's status and provides an event to be notified of status changes.
    /// </summary>
    /// <typeparam name="S">Type of Status to return.</typeparam>
    public interface IEventSinkStatus<S>: IEventSinkStatus where S : IEquatable<S>
    {
        /// <summary>
        /// Returns event sink status.
        /// </summary>
        S Status { get; }
    }
}
