using System.Formats.Asn1;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using KdSoft.EtwEvents.AgentCommand;
using KdSoft.EtwEvents.AgentManager;
using Microsoft.Extensions.Configuration;
using Xunit;
using Xunit.Abstractions;

namespace KdSoft.EtwEvents.Tests
{
    public class CertificateTests
    {
        readonly ITestOutputHelper _output;

        public CertificateTests(ITestOutputHelper output) {
            this._output = output;
        }

        void WriteCertificateInfo(X509Certificate2 certificate, X509Chain chain) {
            _output.WriteLine("{0}======================= CERTIFICATE ======================={0}", Environment.NewLine);

            //Output chain information of the selected certificate.
            //chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            bool valid = chain.Build(certificate);
            _output.WriteLine("Chain Information - valid: {0}", valid);
            _output.WriteLine("Chain revocation flag: {0}", chain.ChainPolicy.RevocationFlag);
            _output.WriteLine("Chain revocation mode: {0}", chain.ChainPolicy.RevocationMode);
            _output.WriteLine("Chain verification flag: {0}", chain.ChainPolicy.VerificationFlags);
            _output.WriteLine("Chain verification time: {0}", chain.ChainPolicy.VerificationTime);
            _output.WriteLine("Chain status length: {0}", chain.ChainStatus.Length);
            _output.WriteLine("Chain application policy count: {0}", chain.ChainPolicy.ApplicationPolicy.Count);
            _output.WriteLine("Chain certificate policy count: {0} {1}", chain.ChainPolicy.CertificatePolicy.Count, Environment.NewLine);

            //Output chain element information.
            _output.WriteLine("Chain Element Information");
            _output.WriteLine("Number of chain elements: {0}", chain.ChainElements.Count);
            _output.WriteLine("Chain elements synchronized? {0}", chain.ChainElements.IsSynchronized);

            foreach (X509ChainElement element in chain.ChainElements) {
                _output.WriteLine("");
                _output.WriteLine("Element Subject: {0}", element.Certificate.Subject);
                _output.WriteLine("Element information: {0}", element.Information);
                _output.WriteLine("Element Signature Algorithm: {0}", element.Certificate.SignatureAlgorithm.FriendlyName);
                _output.WriteLine("Element issuer name: {0}", element.Certificate.Issuer);
                _output.WriteLine("Element certificate is valid: {0}", chain.Build(element.Certificate));
                _output.WriteLine("Element certificate expires after: {0}", element.Certificate.NotAfter);
                _output.WriteLine("Element error status length: {0}", element.ChainElementStatus.Length);
                _output.WriteLine("Number of element extensions: {0}", element.Certificate.Extensions.Count);
                foreach (var ext in element.Certificate.Extensions) {
                    _output.WriteLine("\t{0}:{1}", ext.Oid?.FriendlyName, ext.Format(false));
                }
                if (chain.ChainStatus.Length > 1) {
                    for (int index = 0; index < element.ChainElementStatus.Length; index++) {
                        var cst = element.ChainElementStatus[index];
                        _output.WriteLine("Chain status {0}: {1}", cst.Status, cst.StatusInformation);
                    }
                }
            }
        }

        [Fact]
        public void LoadFromStore() {
            //Create new X509 store from local certificate store.
            X509Store store = new X509Store("MY", StoreLocation.LocalMachine);
            store.Open(OpenFlags.OpenExistingOnly | OpenFlags.ReadWrite);

            //Output store information.
            _output.WriteLine("Store Information");
            _output.WriteLine("Number of certificates in the store: {0}", store.Certificates.Count);
            _output.WriteLine("Store location: {0}", store.Location);
            _output.WriteLine("Store name: {0} {1}", store.Name, Environment.NewLine);

            //Put certificates from the store into a collection so user can select one.
            //X509Certificate2Collection fcollection = (X509Certificate2Collection)store.Certificates;
            //X509Certificate2Collection collection = X509Certificate2UI.SelectFromCollection(fcollection, "Select an X509 Certificate", "Choose a certificate to examine.", X509SelectionFlag.SingleSelection);
            //X509Certificate2 certificate = collection[0];
            //X509Certificate2UI.DisplayCertificate(certificate);

            var certs = CertUtils.GetCertificates(StoreName.My, StoreLocation.LocalMachine, Oids.ClientAuthentication, (Predicate<X509Certificate2>?)null);
            foreach (var certificate in certs) {
                var chain = new X509Chain { ChainPolicy = CertUtils.GetClientCertPolicy() };
                WriteCertificateInfo(certificate, chain);
            }

            store.Close();
        }

        void WriteFileMessage(string filePath, [CallerLineNumber] int lineNo = 0) {
            _output.WriteLine($"Line {lineNo}: {Path.GetFileName(filePath)}");
        }

        [Fact]
        public void InstallCerts() {
            var filesPath = Path.Combine(TestUtils.ProjectDir!, "Files");
            var rootCert = new X509Certificate2(Path.Combine(filesPath, "Kd-Soft.crt"));

            // first uninstall
            using (var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine)) {
                store.Open(OpenFlags.ReadWrite);
                store.Remove(rootCert);
            }

            // cert should not validate without root cert;
            var serverFile = Path.Combine(filesPath, "server.kd-soft.net.p12");
            var serverCert = new X509Certificate2(serverFile, "humpty_dumpty", X509KeyStorageFlags.PersistKeySet);
            var serverChain = new X509Chain { ChainPolicy = new X509ChainPolicy { RevocationMode = X509RevocationMode.NoCheck } };
            bool valid = serverChain.Build(serverCert);
            if (!valid) {
                WriteFileMessage(serverFile);
                foreach (var cst in serverChain.ChainStatus) {
                    _output.WriteLine("\t{0}: {1}", cst.Status, cst.StatusInformation);
                }
            }
            Assert.False(valid);

            var clientFile = Path.Combine(filesPath, "client.p12");
            var clientCert = new X509Certificate2(clientFile, "humpty_dumpty", X509KeyStorageFlags.PersistKeySet);
            var clientChain = new X509Chain { ChainPolicy = new X509ChainPolicy { RevocationMode = X509RevocationMode.NoCheck } };
            valid = clientChain.Build(clientCert);
            if (!valid) {
                WriteFileMessage(clientFile);
                foreach (var cst in clientChain.ChainStatus) {
                    _output.WriteLine("\t{0}: {1}", cst.Status, cst.StatusInformation);
                }
            }
            Assert.False(valid);

            CertUtils.InstallMachineCertificate(rootCert);

            valid = serverChain.Build(serverCert);
            if (!valid) {
                WriteFileMessage(serverFile);
                foreach (var cst in serverChain.ChainStatus) {
                    _output.WriteLine("\t{0}: {1}", cst.Status, cst.StatusInformation);
                }
            }
            Assert.False(valid);

            valid = clientChain.Build(clientCert);
            if (!valid) {
                WriteFileMessage(clientFile);
                foreach (var cst in clientChain.ChainStatus) {
                    _output.WriteLine("\t{0}: {1}", cst.Status, cst.StatusInformation);
                }
            }
            Assert.True(valid);
        }

        [Fact]
        public async Task LoadAgentCerts() {
            var certsDir = new DirectoryInfo(Path.Combine(TestUtils.ProjectDir!, "Certs"));
            var filesDir = new DirectoryInfo(Path.Combine(TestUtils.ProjectDir!, "Files"));

            var proxyLogger = new MockLogger<AgentProxy>(null);
            var proxyMgr = new AgentProxyManager(TimeSpan.FromSeconds(20), proxyLogger);
            var certlogger = new MockLogger<AgentCertificateWatcher>(null);
            using var certMgr = new AgentCertificateWatcher(certsDir, proxyMgr, certlogger);

            var waitTime = certMgr.SettleTime + TimeSpan.FromSeconds(2);
            int fileCount = 0;
            foreach (var file in certsDir.GetFiles()) {
                file.Delete();
            }

            await Task.Delay(TimeSpan.FromSeconds(2));

            foreach (var file in filesDir.GetFiles()) {
                if (file.Name.Contains("-1")) {
                    File.Copy(file.FullName, Path.Combine(certsDir.FullName, file.Name));
                    fileCount += 1;
                }
            }

            await certMgr.StartAsync(CancellationToken.None);

            //await Task.Delay(waitTime);

            foreach (var file in filesDir.GetFiles()) {
                if (file.Name.Contains("-2")) {
                    File.Copy(file.FullName, Path.Combine(certsDir.FullName, file.Name));
                    fileCount += 1;
                }
            }

            await Task.Delay(waitTime);

            foreach (var line in certlogger.FormattedEntries) {
                _output.WriteLine(line);
            }

            Assert.Equal(fileCount, certMgr.Certificates.Count);
        }

        [Fact]
        public async Task CertContentTypes() {
            var filesDir = new DirectoryInfo(Path.Combine(TestUtils.ProjectDir!, "Files"));

            var proxyLogger = new MockLogger<AgentProxy>(null);
            var proxyMgr = new AgentProxyManager(TimeSpan.FromSeconds(20), proxyLogger);
            var certlogger = new MockLogger<AgentCertificateWatcher>(null);
            using var certMgr = new AgentCertificateWatcher(filesDir, proxyMgr, certlogger);

            var waitTime = certMgr.SettleTime + TimeSpan.FromSeconds(2);
            await certMgr.StartAsync(System.Threading.CancellationToken.None);
            await Task.Delay(waitTime);

            foreach (var certEntry in certMgr.Certificates) {
                var cert = certEntry.Value.Item1;
                var ct = X509Certificate2.GetCertContentType(cert.GetRawCertData());
                _output.WriteLine($"{certEntry.Value.Item2}: {ct}");
            }
            _output.WriteLine("========================");
            foreach (var file in filesDir.GetFiles()) {
                try {
                    var ct = X509Certificate2.GetCertContentType(file.FullName);
                    _output.WriteLine($"{file.Name}: {ct}");
                }
                catch (Exception ex) {
                    _output.WriteLine($"{file.Name}: {ex.Message}");
                }
            }
        }

        [Fact]
        public void LoadCerts() {
            var filesDir = new DirectoryInfo(Path.Combine(TestUtils.ProjectDir!, "Files"));
            foreach (var file in filesDir.GetFiles()) {
                if (file.Name.Contains("-1") || file.Name.Contains("-2")) {
                    var fileCert = CertUtils.LoadCertificate(file.FullName, file.FullName, null);
                    var certBytes = File.ReadAllBytes(file.FullName);
                    var bytesCert = CertUtils.LoadCertificate(certBytes);
                    Assert.Equal(fileCert.GetRawCertData(), bytesCert.GetRawCertData());
                }
            }
        }

        [Fact]
        public void CreateServerCertificate() {
            var filesDir = new DirectoryInfo(Path.Combine(TestUtils.ProjectDir!, "Files"));
            var x500Name = new X500DistinguishedName("E=karl@waclawek.net, CN=*.kd-soft.net, OU=ETW, O=Kd-Soft, L=Oshawa, S=ON, C=CA");

            var caFile = Path.Combine(filesDir.FullName, "Kd-Soft.crt");
            var keyFile = Path.Combine(filesDir.FullName, "Kd-Soft.key");
            var caCert = X509Certificate2.CreateFromPemFile(caFile, keyFile);

            var key = CertUtils.CreateAsymmetricKey(caCert);
            var request = CertUtils.CreateCertificateRequest(x500Name, key);
            var cert = CertUtils.CreateServerCertificate(request, caCert);

            var chain = new X509Chain { ChainPolicy = CertUtils.GetServerCertPolicy() };
            WriteCertificateInfo(cert, chain);
            Assert.True(chain.Build(cert));

            caFile = Path.Combine(filesDir.FullName, "Kd-Soft_Test-Signing_CA.pfx");
            var caCerts = new X509Certificate2Collection();
            caCerts.Import(caFile, "dummy");

            key = CertUtils.CreateAsymmetricKey(caCerts.Last());
            request = CertUtils.CreateCertificateRequest(x500Name, key);
            cert = CertUtils.CreateServerCertificate(request, caCerts.Last());

            var chainPolicy = CertUtils.GetServerCertPolicy();
            // we assume the signing certificates in the chain are not installed
            chainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            foreach (var cac in caCerts) {
                if (cac != cert)
                    chainPolicy.CustomTrustStore.Add(cac);
            }
            chain = new X509Chain { ChainPolicy = chainPolicy };
            WriteCertificateInfo(cert, chain);
            Assert.True(chain.Build(cert));
        }

        [Fact]
        public void CreateClientCertificateFromFile() {
            var filesDir = new DirectoryInfo(Path.Combine(TestUtils.ProjectDir!, "Files"));
            //var roleOid = Oid.FromOidValue("2.5.4.72", OidGroup.All); //this throws "The OID value is invalid" for some reason
            var roleOid = new Oid(Oids.Role);
            var x500Name = new X500DistinguishedName($"{roleOid.Value}=etw-admin+{roleOid.Value}=etw-manager, E=karl@waclawek.net, CN=Karl Waclawek, OU=ETW, O=Kd-Soft, L=Oshawa, S=ON, C=CA");

            var caFile = Path.Combine(filesDir.FullName, "Kd-Soft.crt");
            var keyFile = Path.Combine(filesDir.FullName, "Kd-Soft.key");
            var caCert = X509Certificate2.CreateFromPemFile(caFile, keyFile);

            var key = CertUtils.CreateAsymmetricKey(caCert);
            var request = CertUtils.CreateCertificateRequest(x500Name, key);
            var cert = CertUtils.CreateClientCertificate(request, caCert);

            var chain = new X509Chain { ChainPolicy = CertUtils.GetClientCertPolicy() };
            WriteCertificateInfo(cert, chain);
            Assert.True(chain.Build(cert));

            caFile = Path.Combine(filesDir.FullName, "Kd-Soft_Test-Signing_CA.pfx");
            var caCerts = new X509Certificate2Collection();
            caCerts.Import(caFile, "dummy");

            key = CertUtils.CreateAsymmetricKey(caCerts.Last());
            request = CertUtils.CreateCertificateRequest(x500Name, key);
            cert = CertUtils.CreateClientCertificate(request, caCerts.Last());

            var chainPolicy = CertUtils.GetClientCertPolicy();
            // we assume the signing certificates in the chain are not installed
            chainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            foreach (var cac in caCerts) {
                if (cac != cert)
                    chainPolicy.CustomTrustStore.Add(cac);
            }
            chain = new X509Chain { ChainPolicy = chainPolicy };
            WriteCertificateInfo(cert, chain);
            Assert.True(chain.Build(cert));
        }

        [Fact]
        public void ModifyX500DistinguishedName() {
            var roleOid = new Oid(Oids.Role);
            var x500Name = $"{roleOid.Value}=etw-admin+{roleOid.Value}=etw-manager, E=karl@waclawek.net, CN=Karl Waclawek, OU=ETW, O=Kd-Soft, L=Oshawa, S=ON, C=CA";
            var x500Dn = new X500DistinguishedName(x500Name);
            _output.WriteLine(x500Dn.Decode(X500DistinguishedNameFlags.Reversed));

            var rdns = x500Dn.GetRelativeNames();

            var writer = new AsnWriter(AsnEncodingRules.DER);
            CertUtils.WriteRelativeNames(rdns, writer);
            var x500Dn2 = new X500DistinguishedName(writer.Encode());
            _output.WriteLine(x500Dn2.Decode(X500DistinguishedNameFlags.Reversed));

            Assert.Equal<byte>(x500Dn.RawData, x500Dn2.RawData);

            var x500Name3 = $"{roleOid.Value}=etw-admin+{roleOid.Value}=etw-manager+{roleOid.Value}=etw-agent, E=john@mock.de, CN=John Mock, OU=ETW, O=Kd-Soft, L=Oshawa, S=ON, C=CA";
            var x500Dn3 = new X500DistinguishedName(x500Name3);
            _output.WriteLine(x500Dn3.Decode(X500DistinguishedNameFlags.Reversed));

            // build x500Dn4 by modifying x500Dn, to be identical to x500Dn3
            var mergedRdns = CertificateFactory.MergeX500Rdns(rdns, "John Mock", "john@mock.de", "etw-agent");
            writer.Reset();
            CertUtils.WriteRelativeNames(mergedRdns, writer);
            var x500Dn4Intermediate = new X500DistinguishedName(writer.Encode());
            // re-create x500Dn4 to get the canonical oputput for comparison purposes
            var x500Dn4 = new X500DistinguishedName(x500Dn4Intermediate.Decode(X500DistinguishedNameFlags.Reversed));
            _output.WriteLine(x500Dn4.Decode(X500DistinguishedNameFlags.Reversed));

            Assert.Equal<byte>(x500Dn3.RawData, x500Dn4.RawData);
        }

        void CreateClientCertificateFromConfiguration(string configFile) {
            var filesDir = Path.Combine(TestUtils.ProjectDir!, "Files");
            var settingsPath = Path.Combine(filesDir, configFile);
            var cfgBuilder = new ConfigurationBuilder();
            cfgBuilder.AddJsonFile(settingsPath);
            var cfg = cfgBuilder.Build();

            Directory.SetCurrentDirectory(Path.Combine(TestUtils.ProjectDir!, "Files"));

            var certFactory = new CertificateFactory(cfg, () => "dummy");
            var utcNow = DateTimeOffset.UtcNow;
            // round to full seconds
            var now = new DateTimeOffset(utcNow.Ticks - (utcNow.Ticks % TimeSpan.TicksPerSecond), TimeSpan.Zero);

            var cert = certFactory.CreateClientCertificate("Ezechiel Ratcliff", "ezratcliff@test.com", now, 666);
            Assert.Equal("Ezechiel Ratcliff", cert.GetNameInfo(X509NameType.SimpleName, false));
            Assert.Equal("ezratcliff@test.com", cert.GetNameInfo(X509NameType.EmailName, false));
            Assert.Equal(now, new DateTimeOffset(cert.NotBefore.ToUniversalTime(), TimeSpan.Zero));
            Assert.Equal(now + TimeSpan.FromDays(666), new DateTimeOffset(cert.NotAfter.ToUniversalTime(), TimeSpan.Zero));
            Assert.True(cert.HasPrivateKey);

            // CA certificate used to sign the Issuer certificate, which is an intermediate CA
            var caFile = Path.Combine(filesDir, "Kd-Soft.crt");
            var keyFile = Path.Combine(filesDir, "Kd-Soft.key");
            var caCert = X509Certificate2.CreateFromPemFile(caFile, keyFile);

            var chainPolicy = CertUtils.GetClientCertPolicy();
            // we assume the signing certificates in the chain are not installed
            chainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chainPolicy.CustomTrustStore.Add(caCert);
            chainPolicy.CustomTrustStore.Add(certFactory.IssuerCert);
            var chain = new X509Chain { ChainPolicy = chainPolicy };
            WriteCertificateInfo(cert, chain);
            Assert.True(chain.Build(cert));
        }

        [Fact]
        public void CreateClientCertificateFromConfiguration1() {
            var filesDir = Path.Combine(TestUtils.ProjectDir!, "Files");
            var rootCert = new X509Certificate2(Path.Combine(filesDir, "Kd-Soft.crt"));
            CertUtils.InstallMachineCertificate(rootCert);

            CreateClientCertificateFromConfiguration("agentcommand1.appsettings.json");
        }

        [Fact]
        public void CreateClientCertificateFromConfiguration2() {
            var filesDir = Path.Combine(TestUtils.ProjectDir!, "Files");
            var rootCert = new X509Certificate2(Path.Combine(filesDir, "Kd-Soft.crt"));
            CertUtils.InstallMachineCertificate(rootCert);

            CreateClientCertificateFromConfiguration("agentcommand2.appsettings.json");
        }
    }
}
