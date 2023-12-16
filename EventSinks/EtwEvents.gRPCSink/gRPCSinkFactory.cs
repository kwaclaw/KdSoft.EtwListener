using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
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
                HttpClient = new HttpClient(httpHandler, true) { DefaultRequestVersion = HttpVersion.Version20 },
                DisposeHttpClient = true
            });
            return channel;
        }

        static X509Certificate2? GetCertificate(gRPCSinkCredentials creds) {
            if (creds.CertificatePem != null && creds.CertificateKeyPem != null) {
                return X509Certificate2.CreateFromPem(creds.CertificatePem, creds.CertificateKeyPem);
            }
            return CertUtils.GetCertificate(StoreLocation.LocalMachine, creds.CertificateThumbPrint ?? string.Empty, creds.CertificateSubjectCN ?? string.Empty);
        }

        public Task<IEventSink> Create(gRPCSinkOptions options, gRPCSinkCredentials creds, IEventSinkContext context) {
            try {
                var cert = GetCertificate(creds) ?? throw new ArgumentException("Credentials do not specify certificate.");
                var host = options.Host;
                if (string.IsNullOrWhiteSpace(host)) {
                    throw new ArgumentException("Options do not specify host URI.");
                }
                var channel = CreateChannel(host, cert);
                var client = new EtwSinkClient(channel);

                //TODO is this the best way to add the event source/site to the event sink?
                var metaData = new Grpc.Core.Metadata {
                    { "site", context.SiteName }
                };

                var eventStream = client.SendEvents(metaData);

                var result = new gRPCSink(eventStream, context);
                return Task.FromResult((IEventSink)result);
            }
            catch (Exception ex) {
                context.Logger.LogError(ex, "Error in {eventSink} initialization.", nameof(gRPCSink));
                throw;
            }
        }

        public Task<IEventSink> Create(string optionsJson, string credentialsJson, IEventSinkContext context) {
            var options = JsonSerializer.Deserialize<gRPCSinkOptions>(optionsJson, _serializerOptions);
            var creds = JsonSerializer.Deserialize<gRPCSinkCredentials>(credentialsJson, _serializerOptions);
            return Create(options!, creds!, context);
        }

        public string GetCredentialsJsonSchema() {
            return "";
        }

        public string GetOptionsJsonSchema() {
            return "";
        }
    }
}
