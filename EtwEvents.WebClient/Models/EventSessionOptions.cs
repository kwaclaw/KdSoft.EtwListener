using System;

namespace KdSoft.EtwEvents.WebClient.Models
{
    public class EventSessionOptions
    {
        public TimeSpan PushFrequency { get; set; } = TimeSpan.FromMilliseconds(300);
        public int EventQueueCapacity { get; set; } = 128 * 1024;
    }
}
