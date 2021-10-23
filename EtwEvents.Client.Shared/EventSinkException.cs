using System;

namespace KdSoft.EtwEvents.Client.Shared
{
    [Serializable]
    public class EventSinkException: Exception
    {
        public EventSinkException() : base() { }
        public EventSinkException(string message) : base(message) { }
        public EventSinkException(string message, Exception inner) : base(message, inner) { }
    }
}
