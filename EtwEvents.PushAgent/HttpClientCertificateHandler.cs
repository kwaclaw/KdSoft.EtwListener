using System;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace KdSoft.EtwEvents.PushAgent
{
    class HttpClientCertificateHandler: HttpClientHandler
    {
        readonly string _clientCertCN;
        readonly string _clientCertHeader;

        public HttpClientCertificateHandler(string? clientCertCN, string? clientCertHeader) : base() {
            if (clientCertCN == null)
                throw new ArgumentNullException(nameof(clientCertCN));
            if (clientCertHeader == null)
                throw new ArgumentNullException(nameof(clientCertHeader));
            this._clientCertCN = clientCertCN;
            this._clientCertHeader = clientCertHeader;
        }

        // NOTE: The associated root certificate might not be installed, so the client certificate may not be valid locally!
        X509Certificate2? GetClientCertificate() {
            using (var store = new X509Store(StoreName.My, StoreLocation.LocalMachine)) {
                store.Open(OpenFlags.ReadOnly);
                var certs = store.Certificates.Find(X509FindType.FindBySubjectName, _clientCertCN, false);
                foreach (var cert in certs) {
                    if (cert.NotAfter > DateTime.Now)
                        return cert;
                }
                return null;
            }
        }

        string? _encodedClientCertificate;
        string EncodedClientCertificate => _encodedClientCertificate ?? (_encodedClientCertificate = Convert.ToBase64String(GetClientCertificate()?.RawData ?? new byte[0]));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            request.Headers.Add(_clientCertHeader, EncodedClientCertificate);
            return base.SendAsync(request, cancellationToken);
        }
    }
}
