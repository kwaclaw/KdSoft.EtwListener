using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace KdSoft.EtwEvents.PushAgent
{
    public static class Utils
    {
        /// <summary>
        /// Retrieves client certificates matching the specified options.
        /// </summary>
        /// <param name="certOptions">Options to match.</param>
        /// <exception cref="ArgumentException">One of SubjectCN or SubjectRole must specified in the certOptions argument.</exception>
        public static List<X509Certificate2> GetClientCertificates(ClientCertOptions certOptions) {
            if (certOptions.SubjectCN.Length == 0 && certOptions.SubjectRole.Length == 0)
                throw new ArgumentException("Client certificate options must have one of SubjectCN or SubjectRole specified.");

            var result = new List<X509Certificate2>();
            if (certOptions.SubjectCN.Length > 0) {
                var clientCerts = CertUtils.GetCertificates(certOptions.Location, certOptions.SubjectCN, CertUtils.ClientAuthentication);
                result.AddRange(clientCerts);
            }
            if (certOptions.SubjectRole.Length > 0) {
                var clientCerts = CertUtils.GetCertificates(certOptions.Location, CertUtils.ClientAuthentication, crt => {
                    var match = CertUtils.SubjectRoleRegex.Match(crt.Subject);
                    if (match.Success) {
                        var certRole = match.Groups["role"].Value;
                        if (certRole != null && certRole.Equals(certOptions.SubjectRole, System.StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                    return false;
                });
                result.AddRange(clientCerts);
            }

            // sort by descending NotBefore date
            result.Sort((x, y) => DateTime.Compare(y.NotBefore, x.NotBefore));
            return result;
        }

        public static SocketsHttpHandler CreateHttpHandler(params X509Certificate2[] clientCerts) {
            var httpHandler = new SocketsHttpHandler {
                //PooledConnectionLifetime = TimeSpan.FromHours(4),
                SslOptions = new SslClientAuthenticationOptions {
                    ClientCertificates = new X509Certificate2Collection(clientCerts),
                },
            };
            return httpHandler;
        }
    }
}
