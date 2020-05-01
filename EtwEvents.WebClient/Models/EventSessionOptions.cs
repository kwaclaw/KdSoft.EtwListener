using System;

namespace KdSoft.EtwEvents.WebClient.Models
{
    public class EventSessionOptions
    {
        public TimeSpan PushFrequency { get; set; } = TimeSpan.FromMilliseconds(100);
    }
}
