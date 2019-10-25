using System;

namespace EtwEvents.WebClient.Models
{
    public class EventSessionOptions
    {
        public TimeSpan PushFrequency { get; set; } = TimeSpan.FromMilliseconds(100);
    }
}
