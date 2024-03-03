using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Azure.Identity;
using Microsoft.Extensions.Logging;

namespace KdSoft.EtwEvents.EventSinks
{
    [EventSink(nameof(LogsIngestionSink))]
    public partial class LogsIngestionSinkFactory: IEventSinkFactory
    {
        static readonly JsonSerializerOptions _serializerOptions;

        static LogsIngestionSinkFactory() {
            _serializerOptions = new JsonSerializerOptions {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        static X509Certificate2? GetCertificate(LogsIngestionSinkCredentials.ClientCertificateSettings certSettings) {
            if (certSettings.CertificatePem != null && certSettings.CertificateKeyPem != null) {
                return X509Certificate2.CreateFromPem(certSettings.CertificatePem, certSettings.CertificateKeyPem);
            }
            return CertUtils.GetCertificate(StoreName.My, StoreLocation.LocalMachine, certSettings.CertificateThumbPrint ?? string.Empty, certSettings.CertificateSubjectCN ?? string.Empty);
        }

        public static Azure.Core.TokenCredential GetCredential(LogsIngestionSinkCredentials creds) {
            if (string.IsNullOrEmpty(creds.TenantId) ^ string.IsNullOrEmpty(creds.ClientId)) {
                throw new InvalidOperationException("TenantId and ClientId must both be either empty or non-empty.");
            }

            if (string.IsNullOrEmpty(creds.TenantId)) {
                return new DefaultAzureCredential();
            }

            if (creds.ClientSecret is not null) {
                return new ClientSecretCredential(creds.TenantId, creds.ClientId, creds.ClientSecret.Secret);
            }

            if (creds.ClientCertificate is not null) {
                var cert = GetCertificate(creds.ClientCertificate);
                return new ClientCertificateCredential(creds.TenantId, creds.ClientId, cert);
            }

            if (creds.UsernamePassword is not null) {
                return new UsernamePasswordCredential(creds.UsernamePassword.Username, creds.UsernamePassword.Password, creds.TenantId, creds.ClientId);
            }

            throw new InvalidOperationException("No acceptable credentials found.");
        }

        public Task<IEventSink> Create(LogsIngestionSinkOptions options, LogsIngestionSinkCredentials creds, IEventSinkContext context) {
            try {
                var credential = GetCredential(creds);
                return Task.FromResult((IEventSink)new LogsIngestionSink(options, credential, context));
            }
            catch (Exception ex) {
                context.Logger.LogError(ex, "Error in {eventSink} initialization.", nameof(LogsIngestionSink));
                throw;
            }
        }

        public Task<IEventSink> Create(string optionsJson, string credentialsJson, IEventSinkContext context) {
            var options = JsonSerializer.Deserialize<LogsIngestionSinkOptions>(optionsJson, _serializerOptions);
            var creds = JsonSerializer.Deserialize<LogsIngestionSinkCredentials>(credentialsJson, _serializerOptions) ?? new LogsIngestionSinkCredentials();
            return Create(options!, creds, context);
        }

        public string GetCredentialsJsonSchema() {
            return "";
        }

        public string GetOptionsJsonSchema() {
            return "";
        }
    }
}
