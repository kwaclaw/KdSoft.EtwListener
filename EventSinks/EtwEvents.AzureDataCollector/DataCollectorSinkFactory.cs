using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace KdSoft.EtwEvents.EventSinks
{
    [EventSink(nameof(DataCollectorSink))]
    public class DataCollectorSinkFactory: IEventSinkFactory
    {
        static readonly JsonSerializerOptions _serializerOptions;

        static DataCollectorSinkFactory() {
            _serializerOptions = new JsonSerializerOptions {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        // The name of a field in the data that contains the timestamp of the data item.
        // If you specify a field, its contents are used for TimeGenerated.
        // If you don't specify this field, the default for TimeGenerated is the time that the message is ingested.
        // The contents of the message field should follow the ISO 8601 format YYYY-MM-DDThh:mm:ssZ.
        // Note: the Time Generated value cannot be older than 3 days before received time or the row will be dropped.
        const string TimeStampField = ""; // "timeStamp"

        public Task<IEventSink> Create(DataCollectorSinkOptions options, string sharedKey, IEventSinkContext context) {
            var serverUrl = $"https://{options.CustomerId}.ods.opinsights.azure.com/api/logs?api-version=2016-04-01";
            var requestUri = new Uri(serverUrl, UriKind.Absolute);

            HttpClient? http = null;
            SocketsHttpHandler? handler = null;
            try {
                http = handler != null ? new HttpClient(handler) : new HttpClient();
                http.DefaultRequestHeaders.Add("Accept", "application/json");
                http.DefaultRequestHeaders.Add("time-generated-field", TimeStampField);

                // the Log-Type header should indicate the type of record, that is, the set of fields in the event
                var logType = options.LogType;
                if (string.IsNullOrWhiteSpace(logType))
                    logType = "Default";
                // replace invalid characters with '_'
                logType = Regex.Replace(logType, "[^A-Za-z0-9_]", "_");
                // enforce max length constraint by trimming extra length
                if (logType.Length > 100)
                    logType = logType.Substring(0, 100);
                http.DefaultRequestHeaders.Add("Log-Type", logType);

                if (!string.IsNullOrWhiteSpace(options.ResourceId))
                    http.DefaultRequestHeaders.Add("x-ms-AzureResourceId", options.ResourceId);

                return Task.FromResult((IEventSink)new DataCollectorSink(http, requestUri, options, sharedKey, context));
            }
            catch (Exception ex) {
                handler?.Dispose();
                http?.Dispose();
                context.Logger.LogError(ex, "Error in {eventSink} initialization.", nameof(DataCollectorSink));
                throw;
            }
        }

        public Task<IEventSink> Create(string optionsJson, string credentialsJson, IEventSinkContext context) {
            var options = JsonSerializer.Deserialize<DataCollectorSinkOptions>(optionsJson, _serializerOptions);
            var creds = JsonSerializer.Deserialize<DataCollectorSinkCredentials>(credentialsJson, _serializerOptions);
            return Create(options!, creds!.SharedKey, context);
        }

        public string GetCredentialsJsonSchema() {
            return "";
        }

        public string GetOptionsJsonSchema() {
            return "";
        }
    }
}
