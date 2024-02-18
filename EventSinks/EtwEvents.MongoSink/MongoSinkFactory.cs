using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace KdSoft.EtwEvents.EventSinks
{
    [EventSink(nameof(MongoSink))]
    public class MongoSinkFactory: IEventSinkFactory
    {
        static readonly JsonSerializerOptions _serializerOptions;

        static MongoSinkFactory() {
            _serializerOptions = new JsonSerializerOptions {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public Task<IEventSink> Create(MongoSinkOptions options, MongoSinkCredentials creds, IEventSinkContext context) {
            try {
                MongoUrl connectionUrl;
                MongoCredential credential;
                SslSettings? sslSettings = null;
                if (string.IsNullOrWhiteSpace(creds.CertificateCommonName)) {
                    connectionUrl = options.GetConnectionUrl(creds.User, creds.Password);
                    credential = MongoCredential.CreateCredential(creds.Database, creds.User, creds.Password);
                }
                else {
                    connectionUrl = options.GetConnectionUrl();
                    var cert = CertUtils.GetCertificate(StoreName.My, StoreLocation.CurrentUser, string.Empty, creds.CertificateCommonName)
                        ?? CertUtils.GetCertificate(StoreName.My, StoreLocation.LocalMachine, string.Empty, creds.CertificateCommonName)
                        ?? throw new ArgumentException($"Cannot find certificate for common name '{creds.CertificateCommonName}'.");
                    // if provided the user name would have to match the certificate's Subject DN exactly
                    credential = MongoCredential.CreateMongoX509Credential(null);
                    sslSettings = new() {
                        ClientCertificates = new[] { cert },
                        CheckCertificateRevocation = false
                    };
                }

                var mcs = MongoClientSettings.FromUrl(connectionUrl);
                mcs.UseTls = true;
                mcs.Credential = credential;
                if (sslSettings != null)
                    mcs.SslSettings = sslSettings;

                var client = new MongoClient(mcs);
                var db = client.GetDatabase(options.Database);

                var collection = options.Collection.Replace("{site}", context.SiteName);
                var coll = db.GetCollection<BsonDocument>(collection);

                var result = new MongoSink(coll, options.EventFilterFields, options.PayloadFilterFields, context.Logger);
                return Task.FromResult((IEventSink)result);
            }
            catch (Exception ex) {
                context.Logger.LogError(ex, "Error in {eventSink} initialization.", nameof(MongoSink));
                throw;
            }
        }

        public Task<IEventSink> Create(string optionsJson, string credentialsJson, IEventSinkContext context) {
            var options = JsonSerializer.Deserialize<MongoSinkOptions>(optionsJson, _serializerOptions);
            var creds = JsonSerializer.Deserialize<MongoSinkCredentials>(credentialsJson, _serializerOptions);
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
