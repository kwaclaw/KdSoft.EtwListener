using System.Security.Cryptography.X509Certificates;

namespace KdSoft.EtwEvents.EventSinks
{
    public class gRPCSinkCredentials
    {
        public gRPCSinkCredentials(X509Certificate2 certificate) {
            this.Certificate = certificate;
        }

        //TODO do we need to send the root certificates for the client certificate?

        public X509Certificate2 Certificate { get; }
    }
}
