using System.Runtime.CompilerServices;
using System.Text.Json;

namespace KdSoft.EtwLogging
{
    public partial class EtwEvent
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteJson(Utf8JsonWriter jsonWriter) {
            jsonWriter.WriteStartObject();

            jsonWriter.WriteString("providerName", ProviderName);
            jsonWriter.WriteNumber("channel", Channel);
            jsonWriter.WriteNumber("id", Id);
            jsonWriter.WriteNumber("keywords", Keywords);
            jsonWriter.WriteNumber("level", (uint)Level);
            jsonWriter.WriteNumber("opcode", Opcode);
            jsonWriter.WriteString("opcodeName", OpcodeName);
            jsonWriter.WriteString("taskName", TaskName);
            // timeStamp will be passed as milliseconds to Javascript
            if (TimeStamp == null)
                jsonWriter.WriteNull("timeStamp");
            else {
                var timeStamp = (TimeStamp.Seconds * 1000) + (TimeStamp.Nanos / 1000000);
                jsonWriter.WriteNumber("timeStamp", timeStamp);
            }
            jsonWriter.WriteNumber("version", Version);

            jsonWriter.WriteStartObject("payload");
            foreach (var payload in Payload) {
                jsonWriter.WriteString(payload.Key, payload.Value);
            }
            jsonWriter.WriteEndObject();

            jsonWriter.WriteEndObject();
        }


        // Summary:
        //     A name for the event. This is simply the concatenation of the task and opcode
        //     names (separated by a /). If the event has no opcode, then the event name is
        //     just the task name. See TraceEvent.EventName
        internal string _eventName;
        public string EventName {
            get {
                if (_eventName == null) {
                    string text = TaskName;
                    // eventNameIsJustTaskName: (text == "EventWriteString") || (text == "EventID(" + Id.ToString() + ")");
                    if (Opcode == 0 || string.IsNullOrEmpty(OpcodeName) || (text == "EventWriteString") || (text == "EventID(" + Id.ToString() + ")")) {
                        _eventName = text;
                    }
                    else {
                        _eventName = text + "/" + OpcodeName;
                    }
                }

                return _eventName;
            }
        }

    }
}
