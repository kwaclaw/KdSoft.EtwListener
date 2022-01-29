namespace KdSoft.EtwEvents.EventSinks
{
    public class gRPCSinkCredentials
    {
        public gRPCSinkCredentials(byte[] certificateRawData) {
            this.CertificateRawData = certificateRawData;
        }

        public gRPCSinkCredentials(ReadOnlySpan<char> certPem) {
            this.CertificatePem = new string(certPem);
        }

        public gRPCSinkCredentials(ReadOnlySpan<char> certPem, ReadOnlySpan<char> keyPem) {
            this.CertificatePem = new string(certPem);
            this.CertificateKeyPem = new string(keyPem);
        }

        public gRPCSinkCredentials(string thumbPrint) {
            this.ThumbPrint = thumbPrint;
        }

        public gRPCSinkCredentials() {
            this.UseAgentCertificate = true;
        }

        //TODO do we need to send the root certificates for the client certificate?

        public byte[]? CertificateRawData { get; }

        public string? CertificatePem { get; }

        public string? CertificateKeyPem { get; }

        public string? ThumbPrint { get; }

        public bool UseAgentCertificate { get; }
    }
}
