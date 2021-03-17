using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KdSoft.EtwEvents.PushClient {
    public struct ControlEvent {
        public string Event { get; set; }
        public string Id { get; set; }
        public string Data { get; set; }
    }
}
