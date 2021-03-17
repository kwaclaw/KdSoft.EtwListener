﻿using System.Collections.Generic;

namespace KdSoft.EtwEvents.PushClient {
    public class EventSessionOptions {
        public int BatchSize { get; set; } = 100;
        public int MaxWriteDelayMSecs { get; set; } = 400;
        public List<ProviderOptions> Providers { get; set; } = new List<ProviderOptions>();
        public List<string> Filters { get; set; } = new List<string>();
        public int ActiveFilterIndex { get; set; } = -1;
    }
}
