using System;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace KdSoft.EtwEvents.PushAgent
{
    public static class Utils
    {
        public static X509Certificate2? GetClientCertificate(ClientCertOptions certOptions) {
            if (certOptions.SubjectCN.Length == 0 && certOptions.Thumbprint.Length == 0 && certOptions.SubjectRole.Length == 0)
                throw new ArgumentException("Client certificate options must have one of SubjectCN, SubjectRole or Thumbprint specified.");

            X509Certificate2? clientCert = null;
            if (certOptions.Thumbprint.Length > 0) {
                clientCert = CertUtils.GetCertificate(certOptions.Location, certOptions.Thumbprint, string.Empty);
            }
            if (clientCert == null && certOptions.SubjectRole.Length > 0) {
                var clientCerts = CertUtils.GetCertificates(certOptions.Location, CertUtils.ClientAuthentication, crt => {
                    var match = CertUtils.SubjectRoleRegex.Match(crt.Subject);
                    if (match.Success) {
                        var certRole = match.Groups["role"].Value;
                        if (certRole != null && certRole.Equals(certOptions.SubjectRole, System.StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                    return false;
                });
                clientCert = clientCerts.FirstOrDefault();
            }
            if (clientCert == null && certOptions.SubjectCN.Length > 0) {
                clientCert = CertUtils.GetCertificate(certOptions.Location, string.Empty, certOptions.SubjectCN);
            }

            return clientCert;
        }

        public static SocketsHttpHandler CreateHttpHandler(X509Certificate2 clientCert) {
            var httpHandler = new SocketsHttpHandler {
                //PooledConnectionLifetime = TimeSpan.FromHours(4),
                SslOptions = new SslClientAuthenticationOptions {
                    ClientCertificates = new X509Certificate2Collection { clientCert },
                },
            };
            return httpHandler;
        }
    }
}
