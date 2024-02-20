using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;

namespace KdSoft.EtwEvents.AgentCommand
{
    class CertificateFactory: IDisposable
    {
        readonly List<Rdn> _rdns;
        readonly X509Certificate2 _issuerCert;
        readonly ushort _daysValid;

        public CertificateFactory(IConfigurationRoot cfg, Func<string?> getPassword) {
            var x500BaseDn = cfg["DistinguishedBaseName"];
            ArgumentException.ThrowIfNullOrWhiteSpace(x500BaseDn, "DistinguishedBaseName");

            var issuer = cfg.GetRequiredSection("IssuerCertificate");
            if (!issuer.Exists()) {
                throw new ArgumentException("Must provide an issuer certificate", nameof(issuer));
            }

            _daysValid = cfg.GetValue<ushort>("DaysValid", 397);
            _rdns = GetX500Rdns(x500BaseDn, cfg.GetSection("Roles"));

            _issuerCert = LoadX509Certificate(issuer, getPassword);
        }

        public X509Certificate2 IssuerCert => _issuerCert;

        internal static List<Rdn> GetX500Rdns(string x500DN, IConfigurationSection roles) {
            var result = new List<Rdn>();

            var x500Dn = new X500DistinguishedName(x500DN);
            foreach (var rdn in x500Dn.GetRelativeNames()) {
                result.Add(rdn);
            }

            var roleNames = roles.Get<string[]>() ?? Array.Empty<string>();
            if (roleNames.Length > 0) {
                var roleAtts = new List<RdnAttribute>();
                foreach (var roleName in roleNames) {
                    roleAtts.Add(new RdnAttribute(Oids.Role, roleName, System.Formats.Asn1.UniversalTagNumber.UTF8String));
                }
                var roleRdn = new Rdn(roleAtts);
                result.Add(roleRdn);
            }

            return result;
        }

        internal static List<Rdn> MergeX500Rdns(IEnumerable<Rdn> rdns, string commonName, string? email, params string[] roles) {
            var result = new List<Rdn>();
            Rdn? roleRdn = null;

            var rdnByOid = new Dictionary<string, Rdn>(StringComparer.OrdinalIgnoreCase);
            foreach (var rdn in rdns) {
                if (rdn.Attributes.Count == 0) {
                    continue;
                }
                if (rdn.Attributes.Count > 1) {
                    // ignore, we only use that for Role Oids, which we don't process here, as we
                    // assume that this is the Role Rdn, the only multi-element Rdn in our context
                    roleRdn = rdn;

                }
                var rdnAtt = rdn.Attributes[0];
                if (rdnAtt.Oid is not null) {
                    rdnByOid[rdnAtt.Oid] = rdn;
                }
                result.Add(rdn);
            }

            // replace common name
            if (rdnByOid.TryGetValue(Oids.CommonName, out var rdn1)) {
                rdn1.Attributes.Clear();
            }
            else {
                rdn1 = new Rdn(new List<RdnAttribute>());
                result.Add(rdn1);
            }
            rdn1.Attributes.Add(new RdnAttribute(Oids.CommonName, commonName, UniversalTagNumber.UTF8String));

            // replace email address
            if (!string.IsNullOrWhiteSpace(email)) {
                if (rdnByOid.TryGetValue(Oids.EmailAddress, out rdn1)) {
                    rdn1.Attributes.Clear();
                }
                else {
                    rdn1 = new Rdn(new List<RdnAttribute>());
                    result.Add(rdn1);
                }
                rdn1.Attributes.Add(new RdnAttribute(Oids.EmailAddress, email, UniversalTagNumber.IA5String));
            }

            // add more roles
            if (roles?.Length > 0) {
                if (roleRdn is null) {
                    result.Insert(0, roleRdn = new Rdn(new List<RdnAttribute>()));
                }
                foreach (var role in roles) {
                    roleRdn.Attributes.Add(new RdnAttribute(Oids.Role, role, UniversalTagNumber.UTF8String));
                }
            }

            return result;
        }

        X509Certificate2 LoadX509Certificate(IConfigurationSection cfg, Func<string?> getPassword) {
            var path = cfg["Path"];
            var keypath = cfg["KeyPath"];
            if (!string.IsNullOrEmpty(path)) {
                string? pwd = getPassword();
                var cert = CertUtils.LoadCertificate(path, keypath, pwd);
                pwd = null;
                return cert;
            }
            throw new ArgumentException("Missing issuer certificate path.", nameof(cfg));
        }

        public X509Certificate2 CreateClientCertificate(string commonName, string? email, DateTimeOffset startDate = default, ushort? daysValid = null) {
            ArgumentException.ThrowIfNullOrWhiteSpace(commonName, nameof(commonName));

            var rdns = MergeX500Rdns(_rdns, commonName, email);
            var writer = new AsnWriter(AsnEncodingRules.DER);
            CertUtils.WriteRelativeNames(rdns, writer);
            var x500Dn = new X500DistinguishedName(writer.Encode());

            using var key = CertUtils.CreateAsymmetricKey(_issuerCert);
            var request = CertUtils.CreateCertificateRequest(x500Dn, key);
            using var cert = CertUtils.CreateClientCertificate(request, _issuerCert, startDate, daysValid ?? _daysValid);

            if (key is RSA rsa) {
                return cert.CopyWithPrivateKey(rsa);
            }
            return cert.CopyWithPrivateKey((ECDsa)key);
        }

        public void Dispose() => _issuerCert?.Dispose();
    }
}
