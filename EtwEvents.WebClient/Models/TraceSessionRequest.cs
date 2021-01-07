using System;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using KdSoft.EtwLogging;

namespace KdSoft.EtwEvents.WebClient.Models
{
    public class TraceSessionRequest
    {
        [Required(AllowEmptyStrings = false)]
        public string Name { get; set; } = string.Empty;

        [Required(AllowEmptyStrings = false)]
        public string Host { get; set; } = string.Empty;

        [Required]
        public ImmutableArray<ProviderSetting> Providers { get; set; } = ImmutableArray<ProviderSetting>.Empty;

        [Required]
        public TimeSpan LifeTime { get; set; }

        public int BatchSize { get; set; } = 100;

        public TimeSpan MaxWriteDelay { get; set; } = TimeSpan.FromMilliseconds(300);
    }
}
