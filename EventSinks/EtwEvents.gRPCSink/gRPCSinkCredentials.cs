using System;

namespace KdSoft.EtwEvents.EventSinks
{
    public class gRPCSinkCredentials
    {
        public gRPCSinkCredentials(ReadOnlyMemory<byte> certificateRawData) {
            this.CertificateRawData = certificateRawData;
        }

        public gRPCSinkCredentials(ReadOnlySpan<char> certPem) {
            this.CertificatePem = new string(certPem);
        }

        public gRPCSinkCredentials(ReadOnlySpan<char> certPem, ReadOnlySpan<char> keyPem) {
            this.CertificatePem = new string(certPem);
            this.CertificateKeyPem = new string(keyPem);
        }

        public gRPCSinkCredentials(string? thumbPrint, string? subjectCN) {
            if (thumbPrint == null && subjectCN == null)
                throw new ArgumentException("Certificate Thumbprint and Subject CN must not both be null.");
            this.CertificateThumbPrint = thumbPrint;
            this.CertificateSubjectCN = subjectCN;
        }

        //TODO do we need to send the root certificates for the client certificate?

        public ReadOnlyMemory<byte> CertificateRawData { get; }

        public string? CertificatePem { get; }

        public string? CertificateKeyPem { get; }

        public string? CertificateThumbPrint { get; }

        public string? CertificateSubjectCN { get; }
    }
}
