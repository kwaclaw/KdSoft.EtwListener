using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using KdSoft.EtwEvents.Client.Shared;
using KdSoft.EtwLogging;

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

        public async Task<IEventSink> Create(string name, SeqSinkOptions options, string? apiKey = null) {
            var serverUrl = new Uri(options.ServerUrl, UriKind.Absolute);
            var requestUri = new Uri(serverUrl, SeqSink.BulkUploadResource);

            HttpClientHandler? handler = null;
            HttpClient? http = null;
            try {
                if (!string.IsNullOrEmpty(options.ProxyAddress)) {
                    var proxy = new WebProxy(options.ProxyAddress, true);
                    handler = new HttpClientHandler {
                        Proxy = proxy,
                        UseProxy = true
                    };
                }

                http = handler == null ? new HttpClient() : new HttpClient(handler);

                if (!string.IsNullOrWhiteSpace(apiKey))
                    http.DefaultRequestHeaders.Add(SeqSink.ApiKeyHeaderName, apiKey);

                // Get min Seq log level from server, sending an empty event
                var minSeqLevel = await SeqSink.PostAsync(http, requestUri, ReadOnlyMemory<byte>.Empty);
                TraceEventLevel? maxLevel = minSeqLevel == null ? null : SeqSink.FromSeqLogLevel(minSeqLevel.Value);

                return new SeqSink(name, http, requestUri, maxLevel);
            }
            catch {
                handler?.Dispose();
                http?.Dispose();
                throw;
            }
        }

        public Task<IEventSink> Create(string name, string optionsJson, string credentialsJson) {
            var options = JsonSerializer.Deserialize<SeqSinkOptions>(optionsJson, _serializerOptions);
            var creds = JsonSerializer.Deserialize<SeqSinkCredentials>(credentialsJson, _serializerOptions);
            return Create(name, options!, creds!.ApiKey);
        }

        public string GetCredentialsJsonSchema() {
            throw new NotImplementedException();
        }

        public string GetOptionsJsonSchema() {
            throw new NotImplementedException();
        }
    }
}
