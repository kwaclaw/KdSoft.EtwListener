using System;
using System.Net.Http;
using KdSoft.EtwEvents.WebClient.Models;
using shared = KdSoft.EtwEvents.Client.Shared;

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

            this.ClientCertificateOptions = ClientCertificateOption.Manual;
            this.ClientCertificates.Add(clientCert);
        }
    }
}
