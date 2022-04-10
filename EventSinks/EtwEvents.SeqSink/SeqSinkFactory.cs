using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using KdSoft.EtwLogging;
using Microsoft.Extensions.Logging;

namespace KdSoft.EtwEvents.EventSinks
{
    [EventSink(nameof(SeqSink))]
    public class SeqSinkFactory: IEventSinkFactory
    {
        static readonly JsonSerializerOptions _serializerOptions;

        static SeqSinkFactory() {
            _serializerOptions = new JsonSerializerOptions {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public async Task<IEventSink> Create(SeqSinkOptions options, IEventSinkContext context, string? apiKey = null) {
            var serverUrl = new Uri(options.ServerUrl, UriKind.Absolute);
            var requestUri = new Uri(serverUrl, SeqSink.BulkUploadResource);

            HttpClient? http = null;
            SocketsHttpHandler? handler = null;
            try {
                if (!string.IsNullOrEmpty(options.ProxyAddress)) {
                    var proxy = new WebProxy(options.ProxyAddress, true);
                    handler = new SocketsHttpHandler {
                        Proxy = proxy,
                        UseProxy = true
                    };
                }

                http = handler != null ? new HttpClient(handler) : new HttpClient();
                if (!string.IsNullOrWhiteSpace(apiKey))
                    http.DefaultRequestHeaders.Add(SeqSink.ApiKeyHeaderName, apiKey);

                // Get min Seq log level from server, sending an empty event
                var minSeqLevel = await SeqSink.PostAsync(http, requestUri, ReadOnlyMemory<byte>.Empty);
                TraceEventLevel? maxLevel = minSeqLevel == null ? null : SeqSink.FromSeqLogLevel(minSeqLevel.Value);

                return new SeqSink(http, requestUri, maxLevel, context);
            }
            catch (Exception ex) {
                handler?.Dispose();
                http?.Dispose();
                context.Logger.LogError(ex, "Error in {eventSink} initialization.", nameof(SeqSink));
                throw;
            }
        }

        public Task<IEventSink> Create(string optionsJson, string credentialsJson, IEventSinkContext context) {
            var options = JsonSerializer.Deserialize<SeqSinkOptions>(optionsJson, _serializerOptions);
            var creds = JsonSerializer.Deserialize<SeqSinkCredentials>(credentialsJson, _serializerOptions);
            return Create(options!, context, creds!.ApiKey);
        }

        public string GetCredentialsJsonSchema() {
            return "";
        }

        public string GetOptionsJsonSchema() {
            return "";
        }
    }
}
