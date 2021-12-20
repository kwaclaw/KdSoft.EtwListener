using System;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using shared = KdSoft.EtwEvents;

namespace KdSoft.EtwEvents.PushAgent
{
    public static class Utils
    {
        public static SocketsHttpHandler CreateHttpHandler(ClientCertOptions certOptions) {
            if (certOptions.SubjectCN.Length == 0 && certOptions.Thumbprint.Length == 0)
                throw new ArgumentException("Client certificate options must have one of SubjectCN or Thumbprint specified.");
            var clientCert = shared.Utils.GetCertificate(certOptions.Location, certOptions.Thumbprint, certOptions.SubjectCN);
            if (clientCert == null)
                throw new ArgumentException("Cannot find certificate based on specified options.", nameof(certOptions));

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
