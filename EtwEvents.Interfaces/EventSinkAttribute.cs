using System;

namespace KdSoft.EtwEvents.Client.Shared
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class EventSinkAttribute: Attribute
    {
        public EventSinkAttribute(string sinkType) {
            this.SinkType = sinkType;
        }

        public string SinkType { get; }
    }
}
