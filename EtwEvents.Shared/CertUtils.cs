using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace KdSoft.EtwEvents
{
    public record RdnAttribute(Oid Type, string Value);

    public record Rdn(ICollection<RdnAttribute> Attributes);


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

        public static List<string> GetSubjectRoles(this X509Certificate2 cert) {
            var rdns = cert.SubjectName.GetRelativeNames();
            var result = new List<string>();
            var roleOID = new Oid("2.5.4.72");
            foreach (var rdn in rdns) {
                foreach (var att in rdn.Attributes) {
                    if (att.Type.Value == roleOID.Value)
                        result.Add(att.Value);
                }
            }
            return result;
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

        public static X509Certificate2 LoadCertificate(string filePath) {
            X509ContentType contentType;
            try {
                contentType = X509Certificate2.GetCertContentType(filePath);
            }
            catch (CryptographicException) {
                contentType = X509ContentType.Unknown;
            }
            switch (contentType) {
                // we assume it is a PEM certificate with the unencrypted private key included
                case X509ContentType.Unknown:
                    return X509Certificate2.CreateFromPemFile(filePath, filePath);
                case X509ContentType.Cert:
                    return new X509Certificate2(filePath);
                case X509ContentType.Pfx:
                    return new X509Certificate2(filePath, (string?)null, X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
                default:
                    throw new ArgumentException($"Unrecognized certificate type in file: {filePath}");
            }
        }

        public static X509Certificate2 LoadCertificate(ReadOnlySpan<byte> rawData, Encoding? encoding = null) {
            X509ContentType contentType;
            try {
                contentType = X509Certificate2.GetCertContentType(rawData);
            }
            catch (CryptographicException) {
                contentType = X509ContentType.Unknown;
            }
            switch (contentType) {
                // we assume it is a PEM certificate with the unencrypted private key included
                case X509ContentType.Unknown:
                    var charSpan = (encoding ?? Encoding.Default).GetString(rawData);
                    return X509Certificate2.CreateFromPem(charSpan, charSpan);
                case X509ContentType.Cert:
                    return new X509Certificate2(rawData);
                case X509ContentType.Pfx:
                    return new X509Certificate2(rawData, (string?)null, X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
                default:
                    throw new ArgumentException($"Unrecognized certificate type.");
            }
        }


        static AsymmetricAlgorithm? GetPrivateKey(X509Certificate2 cert, out bool isRSA) {
            const string RSA = "1.2.840.113549.1.1.1";
            const string ECC = "1.2.840.10045.2.1";
            isRSA = false;
            switch (cert.PublicKey.Oid.Value) {
                case RSA:
                    isRSA = true;
                    return cert.GetRSAPrivateKey();
                case ECC:
                    return cert.GetECDsaPrivateKey();
                default:
                    return null;
            }
        }

        static void ExportToPEM(StringBuilder builder, X509Certificate2 cert, bool exportPrivateKey) {
            if (exportPrivateKey) {
                var privateKey = GetPrivateKey(cert, out var isRSA);
                if (privateKey is not null) {
                    if (isRSA && privateKey is RSA rsaKey) {
                        rsaKey.ExportParameters(true);
                        var rsaKeyPEM = PemEncoding.Write("RSA PRIVATE KEY", rsaKey.ExportRSAPrivateKey());
                        //var rsaKeyPEM = PemEncoding.Write("RSA PRIVATE KEY", rsaKey.ExportPkcs8PrivateKey());
                        builder.Append(rsaKeyPEM).AppendLine();
                    }
                    else if (privateKey is ECDsa ecdsaKey) {
                        ecdsaKey.ExportParameters(true);
                        ecdsaKey.ExportExplicitParameters(true);
                        var ecdsaKeyPEM = PemEncoding.Write("ECP PRIVATE KEY", ecdsaKey.ExportECPrivateKey());
                        //var ecdsaKeyPEM = PemEncoding.Write("ECP PRIVATE KEY", ecdsaKey.ExportPkcs8PrivateKey());
                        builder.Append(ecdsaKeyPEM).AppendLine();
                    }
                }
            }
            var certPEM = PemEncoding.Write("CERTIFICATE", cert.GetRawCertData());
            builder.Append(certPEM);
        }

        /// <summary>
        /// Export a certificate to a PEM format string.
        /// </summary>
        /// <param name="cert">The certificate to export.</param>
        /// <returns>A PEM encoded string.</returns>
        public static string ExportToPEM(X509Certificate2 cert, bool exportPrivateKey = true) {
            StringBuilder builder = new StringBuilder();
            ExportToPEM(builder, cert, exportPrivateKey);
            return builder.ToString();
        }

        /// <summary>
        /// Export multiple certificates to a PEM format string.
        /// </summary>
        /// <param name="certs">The certificates to export.</param>
        /// <returns>A PEM encoded string.</returns>
        public static string ExportToPEM(IEnumerable<X509Certificate2> certs, bool exportPrivateKey = true) {
            StringBuilder builder = new StringBuilder();
            foreach (var cert in certs) {
                ExportToPEM(builder, cert, exportPrivateKey);
            }
            return builder.ToString();
        }

        /// <summary>
        /// Extracts the Relative Distinguished Names (RDNs) from a Distinguished Name (DN).
        /// Supports multi-valued RDNs. Does not support values other than string types.
        /// </summary>
        /// <param name="distinguishedName"></param>
        /// <returns>Collection of RDNS (Pairs of OID and Value).</returns>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        public static IEnumerable<Rdn> GetRelativeNames(this X500DistinguishedName distinguishedName) {
            var reader = new AsnReader(distinguishedName.RawData, AsnEncodingRules.BER);
            var snSeq = reader.ReadSequence();
            if (!snSeq.HasData) {
                throw new InvalidOperationException();
            }
            // Many types are allowable.  We're only going to support the string-like ones
            // (This excludes IPAddress, X400 address, and other weird stuff)
            // https://www.rfc-editor.org/rfc/rfc5280#page-37
            // https://www.rfc-editor.org/rfc/rfc5280#page-112
            var allowedRdnTags = new[] {
                UniversalTagNumber.TeletexString,
                UniversalTagNumber.PrintableString,
                UniversalTagNumber.UniversalString,
                UniversalTagNumber.UTF8String,
                UniversalTagNumber.BMPString,
                UniversalTagNumber.IA5String,
                UniversalTagNumber.NumericString,
                UniversalTagNumber.VisibleString,
                UniversalTagNumber.T61String
            };
            while (snSeq.HasData) {
                var rdnSet = snSeq.ReadSetOf();
                var rdnAttributes = new List<RdnAttribute>();
                while (rdnSet.HasData) {
                    var rdnSeq = rdnSet.ReadSequence();
                    while (rdnSeq.HasData) {
                        var attrOid = rdnSeq.ReadObjectIdentifier();
                        var attrValueTagNo = (UniversalTagNumber)rdnSeq.PeekTag().TagValue;
                        if (!allowedRdnTags.Contains(attrValueTagNo)) {
                            throw new NotSupportedException($"Unknown tag type {attrValueTagNo} for attr {attrOid}");
                        }
                        var attrValue = rdnSeq.ReadCharacterString(attrValueTagNo);
                        rdnAttributes.Add(new RdnAttribute(new Oid(attrOid), attrValue));
                    }
                }
                yield return new Rdn(rdnAttributes);
            }
        }
    }
}
