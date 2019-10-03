using System;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using KdSoft.EtwLogging;

namespace EtwEvents.WebClient.Models
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
    }
}
