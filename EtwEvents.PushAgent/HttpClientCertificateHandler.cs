using System;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using shared = KdSoft.EtwEvents.Client;

namespace KdSoft.EtwEvents.PushAgent
{
    class HttpClientCertificateHandler: HttpClientHandler
    {
        public HttpClientCertificateHandler(ClientCertOptions certOptions) : base() {
            if (certOptions.SubjectCN.Length == 0 && certOptions.Thumbprint.Length == 0)
                throw new ArgumentException("Client certificate options must have one of SubjectCN or Thumbprint specified.");
            var clientCert = shared.Utils.GetCertificate(certOptions.Location, certOptions.Thumbprint, certOptions.SubjectCN);
            if (clientCert == null)
                throw new ArgumentException("Cannot find certificate based on specified options.", nameof(certOptions));

            this.ClientCert = clientCert;

            this.ClientCertificateOptions = ClientCertificateOption.Manual;
            this.ClientCertificates.Add(clientCert);
        }

        public X509Certificate2 ClientCert { get; }
    }
}
