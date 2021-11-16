using System;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using shared = KdSoft.EtwEvents.Client;

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

        /// <summary>
        /// Logs exception drilling down into inner exceptions and base exceptions.
        /// </summary>
        /// <param name="logger">Logger instance.</param>
        /// <param name="ex">Exception to log.</param>
        /// <param name="message">Error message.</param>
        public static void LogAllErrors(this ILogger logger, Exception ex, string? message = null) {
            if (logger == null)
                return;
            if (ex is AggregateException aggex) {
                foreach (var iex in aggex.Flatten().InnerExceptions) {
                    logger.LogError(iex.GetBaseException(), message ?? iex.Message);
                }
            }
            else {
                logger.LogError(ex.GetBaseException(), message ?? ex.Message);
            }
        }
    }
}
