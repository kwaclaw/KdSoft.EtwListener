using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Schema.Generation;
using static KdSoft.EtwLogging.EtwSink;

namespace KdSoft.EtwEvents.EventSinks
{
    [EventSink(nameof(gRPCSink))]
    public class gRPCSinkFactory: IEventSinkFactory
    {
        static readonly JsonSerializerOptions _serializerOptions;

        static gRPCSinkFactory() {
            _serializerOptions = new JsonSerializerOptions {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        //TODO how to add other credentials (using Composite call credential) - see https://grpc.io/docs/guides/auth/
        // also - https://docs.microsoft.com/en-us/aspnet/core/grpc/authn-and-authz?view=aspnetcore-6.0

        static GrpcChannel CreateChannel(string host, params X509Certificate2[] clientCertificates) {
            var httpHandler = new SocketsHttpHandler {
                //PooledConnectionLifetime = TimeSpan.FromHours(4),
                SslOptions = new SslClientAuthenticationOptions {
                    ClientCertificates = new X509Certificate2Collection(clientCertificates),
                },
            };

            var channel = GrpcChannel.ForAddress(host, new GrpcChannelOptions {
                Credentials = Grpc.Core.ChannelCredentials.SecureSsl,
                HttpClient = new HttpClient(httpHandler, true),
                DisposeHttpClient = true
            });
            return channel;
        }


        public Task<IEventSink> Create(gRPCSinkOptions options, gRPCSinkCredentials creds, ILogger logger) {
            try {
                var channel = CreateChannel(options.Host, creds.Certificate);
                var client = new EtwSinkClient(channel);
                var eventStream = client.SendEvents();

                var result = new gRPCSink(eventStream, logger);
                return Task.FromResult((IEventSink)result);
            }
            catch (Exception ex) {
                logger.LogError(ex, "Error in {eventSink} initialization.", nameof(gRPCSink));
                throw;
            }
        }

        public Task<IEventSink> Create(string optionsJson, string credentialsJson, ILogger logger) {
            var options = JsonSerializer.Deserialize<gRPCSinkOptions>(optionsJson, _serializerOptions);
            var creds = JsonSerializer.Deserialize<gRPCSinkCredentials>(credentialsJson, _serializerOptions);
            return Create(options!, creds!, logger);
        }

        static string GetJsonSchema<T>() {
            var generator = new JSchemaGenerator();
            var schema = generator.Generate(typeof(T));
            return schema.ToString();
        }

        public string GetCredentialsJsonSchema() {
            return GetJsonSchema<gRPCSinkCredentials>();
        }

        public string GetOptionsJsonSchema() {
            return GetJsonSchema<gRPCSinkOptions>();
        }
    }
}
