using System;

namespace KdSoft.EtwEvents.Client.Shared
{
    [AttributeUsage(AttributeTargets.Class)]
    public class EventSinkAttribute: Attribute
    {
        public EventSinkAttribute(string name) {
            this.Name = name;
        }

        public string Name { get; }
    }
}
