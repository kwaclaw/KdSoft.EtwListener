using System;

namespace KdSoft.EtwEvents.EventSinks
{
    public class gRPCSinkCredentials
    {
        public gRPCSinkCredentials() { }

        public gRPCSinkCredentials(ReadOnlySpan<char> certPem) {
            CertificatePem = new string(certPem);
        }

        public gRPCSinkCredentials(ReadOnlySpan<char> certPem, ReadOnlySpan<char> keyPem) {
            CertificatePem = new string(certPem);
            CertificateKeyPem = new string(keyPem);
        }

        public gRPCSinkCredentials(string? thumbPrint, string? subjectCN) {
            if (thumbPrint == null && subjectCN == null)
                throw new ArgumentException("Certificate Thumbprint and Subject CN must not both be null.");
            CertificateThumbPrint = thumbPrint;
            CertificateSubjectCN = subjectCN;
        }

        public string? CertificatePem { get; set; }

        public string? CertificateKeyPem { get; set; }

        public string? CertificateThumbPrint { get; set; }

        public string? CertificateSubjectCN { get; set; }
    }
}
