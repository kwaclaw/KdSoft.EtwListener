using Google.Protobuf.WellKnownTypes;
using Microsoft.Diagnostics.Tracing;

namespace KdSoft.EtwLogging
{
    public partial class EtwEvent
    {
        internal EtwEvent(TraceEvent evt) : this() {
            SetTraceEvent(evt);
        }

        internal void SetTraceEvent(TraceEvent evt) {
            this.ProviderName = evt.ProviderName;
            this.Channel = (uint)evt.Channel;
            this.Id = (uint)evt.ID;
            this.Keywords = (long)evt.Keywords;
            this.Level = (TraceEventLevel)evt.Level;
            this.Opcode = (uint)evt.Opcode;
            this.OpcodeName = evt.OpcodeName;
            this.TaskName = evt.TaskName;
            this.TimeStamp = evt.TimeStamp.ToUniversalTime().ToTimestamp();
            this.Version = evt.Version;
            for (int indx = 0; indx < evt.PayloadNames.Length; indx++) {
                var propName = evt.PayloadNames[indx];
                this.Payload[propName] = evt.PayloadString(indx);
            }
        }
    }
}
