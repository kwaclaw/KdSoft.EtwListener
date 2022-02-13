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
    }
}
