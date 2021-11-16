using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using KdSoft.EtwEvents.Client;
using KdSoft.EtwLogging;
using Microsoft.Extensions.Logging;

namespace KdSoft.EtwEvents.EventSinks
{
    [EventSink(nameof(SeqSink))]
    public class SeqSinkFactory: IEventSinkFactory
    {
        static readonly JsonSerializerOptions _serializerOptions;

        static SeqSinkFactory() {
            var evtSinkAssembly = Assembly.GetExecutingAssembly();
            var evtSinkDir = Path.GetDirectoryName(evtSinkAssembly.Location);
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) => Utils.DirectoryResolveAssembly(evtSinkDir!, args);

            _serializerOptions = new JsonSerializerOptions {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public async Task<IEventSink> Create(SeqSinkOptions options, ILogger logger, string? apiKey = null) {
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

                return new SeqSink(http, requestUri, maxLevel, logger);
            }
            catch (Exception ex) {
                handler?.Dispose();
                http?.Dispose();
                logger.LogError(ex, $"Error in {nameof(SeqSink)} initialization.");
                throw;
            }
        }

        public Task<IEventSink> Create(string optionsJson, string credentialsJson, ILogger logger) {
            var options = JsonSerializer.Deserialize<SeqSinkOptions>(optionsJson, _serializerOptions);
            var creds = JsonSerializer.Deserialize<SeqSinkCredentials>(credentialsJson, _serializerOptions);
            return Create(options!, logger, creds!.ApiKey);
        }

        public string GetCredentialsJsonSchema() {
            throw new NotImplementedException();
        }

        public string GetOptionsJsonSchema() {
            throw new NotImplementedException();
        }
    }
}
