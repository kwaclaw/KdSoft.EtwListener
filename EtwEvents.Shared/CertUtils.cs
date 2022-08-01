using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

namespace KdSoft.EtwEvents
{
    public static class CertUtils
    {
        /// <summary>
        /// Checks if a certificate supports a given enhanced key usage.
        /// </summary>
        /// <param name="cert">Certificate to check.</param>
        /// <param name="oid">Oid identifier for enhanced key usage.</param>
        /// <remarks>If a certificate has no EKUs, then it supports all of them.</remarks>
        public static bool SupportsEnhancedKeyUsage(this X509Certificate2 cert, string oid) {
            var ekuCount = 0;
            foreach (var ext in cert.Extensions) {
                var ekus = ext as X509EnhancedKeyUsageExtension;
                if (ekus is not null) {
                    foreach (var eku in ekus.EnhancedKeyUsages) {
                        ekuCount++;
                        if (eku.Value == oid)
                            return true;
                    }
                }
            }
            // if no ekus are present then all are present
            return ekuCount == 0;
        }

        /// <summary>
        /// Get certificate from certificate store based on thumprint or subject common name.
        /// If multiple certificates match then the one with the newest NotBefore date is returned.
        /// </summary>
        /// <param name="location">Store location.</param>
        /// <param name="thumbprint">Certificate thumprint to look for. Takes precedence over subjectCN when both are specified.</param>
        /// <param name="subjectCN">Subject common name to look for.</param>
        /// <param name="ekus">Enhanced key usage identifiers, all of which the certificate must support.
        /// Applies only when looking for a match on <paramref name="subjectCN"/>.</param>
        /// <returns>Matching certificate, or <c>null</c> if none was found.</returns>
        public static X509Certificate2? GetCertificate(StoreLocation location, string thumbprint, string subjectCN, params string[] ekus) {
            if (thumbprint.Length == 0 && subjectCN.Length == 0)
                return null;

            // find matching certificate, use thumbprint if available, otherwise use subject common name (CN)
            using (var store = new X509Store(location)) {
                store.Open(OpenFlags.ReadOnly);
                X509Certificate2? cert = null;
                if (thumbprint.Length > 0) {
                    var certs = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, true);
                    if (certs.Count > 0)
                        cert = certs.OrderByDescending(cert => cert.NotBefore).First();
                }
                else {
                    var certs = store.Certificates.Find(X509FindType.FindBySubjectName, subjectCN, true);
                    foreach (var matchingCert in certs) {
                        // X509NameType.SimpleName extracts CN from subject (common name)
                        var cn = matchingCert.GetNameInfo(X509NameType.SimpleName, false);
                        if (string.Equals(cn, subjectCN, StringComparison.InvariantCultureIgnoreCase)) {
                            var matchesEkus = true;
                            foreach (var oid in ekus) {
                                matchesEkus = matchesEkus && matchingCert.SupportsEnhancedKeyUsage(oid);
                            }
                            if (matchesEkus) {
                                if (cert == null)
                                    cert = matchingCert;
                                else if (cert.NotBefore < matchingCert.NotBefore)
                                    cert = matchingCert;
                            }
                        }
                    }
                }
                return cert;
            }
        }

        /// <summary>
        /// Get certificates from certificate store based on subject common name and optional enhanced key usage.
        /// The resulting collection is unordered.
        /// </summary>
        /// <param name="location">Store location.</param>
        /// <param name="subjectCN">Subject common name to look for, comparison is not case sensitive.</param>
        /// <param name="ekus">Enhanced key usage identifiers, all of which the certificate must support.</param>
        /// <returns>Matching certificates, or an empty collection if none were found.</returns>
        public static IEnumerable<X509Certificate2> GetCertificates(StoreLocation location, string subjectCN, params string[] ekus) {
            // find matching certificate, use thumbprint if available, otherwise use subject common name (CN)
            using (var store = new X509Store(location)) {
                store.Open(OpenFlags.ReadOnly);
                var certs = store.Certificates.Find(X509FindType.FindBySubjectName, subjectCN, true);
                foreach (var matchingCert in certs) {
                    // X509NameType.SimpleName extracts CN from subject (common name)
                    var cn = matchingCert.GetNameInfo(X509NameType.SimpleName, false);
                    if (string.Equals(cn, subjectCN, StringComparison.InvariantCultureIgnoreCase)) {
                        var matchesEkus = true;
                        foreach (var oid in ekus) {
                            matchesEkus = matchesEkus && matchingCert.SupportsEnhancedKeyUsage(oid);
                        }
                        if (matchesEkus) {
                            yield return matchingCert;
                        }
                    }
                }
            }
        }

        // it seems that the same DN component can be encoded by OID or OID's friendly name, e.g. "OID.2.5.4.72", "2.5.4.72" or "role"
        public static Regex SubjectRoleRegex = new Regex(@"(OID\.2\.5\.4\.72|\.2\.5\.4\.72|role)\s*=\s*(?<role>[^,=]*)\s*(,|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static string? GetSubjectRole(this X509Certificate2 cert) {
            string? certRole = null;
            var match = SubjectRoleRegex.Match(cert.Subject);
            if (match.Success) {
                certRole = match.Groups["role"].Value;
            }
            return certRole;
        }

        public const string ClientAuthentication = "1.3.6.1.5.5.7.3.2";
        public const string ServerAuthentication = "1.3.6.1.5.5.7.3.1";

        /// <summary>
        /// Get certificates from certificate store based on application policy OID and predicate callback.
        /// The resulting collection is unordered.
        /// </summary>
        /// <param name="location">Store location.</param>
        /// <param name="policyOID">Application policy OID to look for, e.g. Client Authentication (1.3.6.1.5.5.7.3.2). Required.</param>
        /// <param name="predicate">Callback to check certificate against a condition. Optional.</param>
        /// <returns>Matching certificates, or an empty collection if none are found.</returns>
        public static IEnumerable<X509Certificate2> GetCertificates(StoreLocation location, string policyOID, Predicate<X509Certificate2>? predicate) {
            if (policyOID.Length == 0)
                return Enumerable.Empty<X509Certificate2>();

            using (var store = new X509Store(location)) {
                store.Open(OpenFlags.ReadOnly);
                var certs = store.Certificates.Find(X509FindType.FindByApplicationPolicy, policyOID, true);
                if (certs.Count == 0 || predicate == null)
                    return certs;
                return certs.Where(crt => predicate(crt));
            }
        }

        /// <summary>
        /// Returns <see cref="X509ChainPolicy"/> that can be used for client certificate validation.
        /// </summary>
        /// <param name="checkRevocation">Indicates if the certificate chain must be checked for revocation.
        ///     The default is <c>false</c>. Should not be turned on for self-signed certificates,
        ///     as they cannot be checked for revocation.</param>
        public static X509ChainPolicy GetClientCertPolicy(bool checkRevocation = false) {
            var result = new X509ChainPolicy();
            // Enhanced Key Usage: Client Validation
            result.ApplicationPolicy.Add(Oid.FromOidValue(ClientAuthentication, OidGroup.EnhancedKeyUsage));
            if (!checkRevocation)
                result.RevocationMode = X509RevocationMode.NoCheck;
            return result;
        }

        /// <summary>
        /// Returns <see cref="X509ChainPolicy"/> that can be used for server certificate validation.
        /// </summary>
        /// <param name="checkRevocation">Indicates if the certificate chain must be checked for revocation.
        ///     The default is <c>false</c>. Should not be turned on for self-signed certificates,
        ///     as they cannot be checked for revocation.</param>
        public static X509ChainPolicy GetServerCertPolicy(bool checkRevocation = false) {
            var result = new X509ChainPolicy();
            // Enhanced Key Usage: Client Validation
            result.ApplicationPolicy.Add(Oid.FromOidValue(ServerAuthentication, OidGroup.EnhancedKeyUsage));
            if (!checkRevocation)
                result.RevocationMode = X509RevocationMode.NoCheck;
            return result;
        }

        /// <summary>
        /// Determines if the certificate is self signed.
        /// </summary>
        /// <param name="certificate">The <see cref="X509Certificate2"/> to check.</param>
        /// <returns><c>true</c> if the certificate is self signed, <c>false</c> otherwise.</returns>
        public static bool IsSelfSigned(this X509Certificate2 certificate) {
            var subjectRaw = certificate.SubjectName.RawData;
            var issuerRaw = certificate.IssuerName.RawData;
            return subjectRaw.SequenceEqual(issuerRaw);
        }

        /// <summary>
        /// Install the certificate in the LocalMachine scope, selecting the store based on the certificate type.
        /// </summary>
        /// <param name="certificate">The <see cref="X509Certificate2"/> to install.</param>
        public static void InstallMachineCertificate(X509Certificate2 certificate) {
            var storeName = StoreName.My;
            var basicConstraintExt = certificate.Extensions["2.5.29.19"] as X509BasicConstraintsExtension;
            if (basicConstraintExt != null) {
                if (basicConstraintExt.CertificateAuthority) {
                    if (certificate.IsSelfSigned())
                        storeName = StoreName.Root;  // root CA
                    else
                        storeName = StoreName.CertificateAuthority;  // intermediate CA
                }
            }
            using var store = new X509Store(storeName, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadWrite);
            store.Add(certificate);
        }
    }
}
