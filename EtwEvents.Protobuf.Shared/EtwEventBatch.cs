using System.Text.Json;

namespace KdSoft.EtwLogging
{
    public partial class EtwEventBatch
    {
        public void WriteJsonArray(Utf8JsonWriter jsonWriter) {
            jsonWriter.WriteStartArray();
            foreach (var evt in Events) {
                evt.WriteJson(jsonWriter);
            }
            jsonWriter.WriteEndArray();
        }

        public void WriteJsonEvents(Utf8JsonWriter jsonWriter) {
            foreach (var evt in Events) {
                evt.WriteJson(jsonWriter);
            }
        }
    }
}