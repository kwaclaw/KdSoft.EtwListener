using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;

namespace EtwEvents.WebClient
{
    /// <summary>
    /// The new Json.NET doesn't support Timespan at this time
    /// https://github.com/dotnet/corefx/issues/38641
    /// </summary>
    public class TimeSpanISO8601JsonConverter: JsonConverter<TimeSpan>
    {
        public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            var s = reader.GetString();
            if (string.IsNullOrWhiteSpace(s)) {
                return TimeSpan.Zero;
            }
            else {
                try {
                    return XmlConvert.ToTimeSpan(s);
                }
                catch (Exception ex) {
                    // throwing a JsonException is necessary for the infrastructure to return the proper ProblemDetails type
                    throw new JsonException(ex.Message, ex);
                }
            }
        }

        public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options) {
            writer.WriteStringValue(XmlConvert.ToString(value));
        }
    }

    public class NullableTimeSpanISO8601JsonConverter: JsonConverter<TimeSpan?>
    {
        public override TimeSpan? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            var s = reader.GetString();
            if (string.IsNullOrWhiteSpace(s)) {
                return (TimeSpan?)null;
            }
            else {
                try {
                    return XmlConvert.ToTimeSpan(s);
                }
                catch (Exception ex) {
                    throw new JsonException(ex.Message, ex);
                }
            }
        }

        public override void Write(Utf8JsonWriter writer, TimeSpan? value, JsonSerializerOptions options) {
            if (value == null)
                writer.WriteNullValue();
            else
                writer.WriteStringValue(XmlConvert.ToString(value.Value));
        }
    }
}
