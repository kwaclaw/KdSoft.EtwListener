using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using KdSoft.Utils;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KdSoft.EtwEvents.AgentManager
{
    public class AgentCertificateWatcher: BackgroundService
    {
        readonly DirectoryInfo _dirInfo;
        readonly AgentProxyManager _agentProxyMgr;
        readonly ILogger<AgentCertificateWatcher> _logger;
        readonly FileChangeDetector _fileChangeDetector;

        readonly ImmutableArray<string> pemPatterns = ImmutableArray.Create<string>("*.pem", "*.crt", "*.cer");
        readonly ImmutableArray<string> pfxPatterns = ImmutableArray.Create<string>("*.pfx", "*.p12");

        CancellationTokenRegistration? _stopRegistration;

        ImmutableDictionary<string, (X509Certificate2, string)> _certificates;
        public ImmutableDictionary<string, (X509Certificate2, string)> Certificates => _certificates;

        public readonly TimeSpan SettleTime = TimeSpan.FromSeconds(5);

        public AgentCertificateWatcher(DirectoryInfo dirInfo, AgentProxyManager agentProxyMgr, ILogger<AgentCertificateWatcher> logger) {
            this._dirInfo = dirInfo;
            this._agentProxyMgr = agentProxyMgr;
            this._logger = logger;
            var nf = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.LastWrite | NotifyFilters.Size;
            var searchPatterns = pemPatterns.AddRange(pfxPatterns);
            _fileChangeDetector = new FileChangeDetector(dirInfo.FullName, searchPatterns, false, nf, SettleTime);
            _fileChangeDetector.FileChanged += FileChanged;
            _fileChangeDetector.ErrorEvent += FileChangeError;
            _certificates = ImmutableDictionary<string, (X509Certificate2, string)>.Empty;
        }

        public X509Certificate2 LoadCertificate(string filePath) {
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
                    return new X509Certificate2(filePath, (string?)null, X509KeyStorageFlags.PersistKeySet);
                default:
                    throw new ArgumentException($"Unrecognized certificate type in file: {filePath}");
            }
        }

        /// <summary>
        /// Removes certificate from loaded certificates.
        /// </summary>
        /// <param name="commonName">Common name to identify certificate.</param>
        /// <param name="cert">Certificate to remove.</param>
        /// <returns><c>true</c> if certificate was successfully removed, <c>false</c> otherwise.</returns>
        public bool TryRemoveCertificate(string commonName, out X509Certificate2 cert) {
            (X509Certificate2, string) entry;
            var result = ImmutableInterlocked.TryRemove(ref _certificates, commonName, out entry);
            cert = entry.Item1;
            return result;
        }

        /// <summary>
        /// Loads certificates from directory. Certificate files must not be password protected.
        /// </summary>
        public void LoadCertificates() {
            var certs = ImmutableDictionary<string, (X509Certificate2, string)>.Empty;
            foreach (var pemPattern in pemPatterns) {
                foreach (var file in _dirInfo.GetFiles(pemPattern, SearchOption.TopDirectoryOnly)) {
                    try {
                        var cert = X509Certificate2.CreateFromPemFile(file.FullName);
                        certs = certs.Add(cert.GetNameInfo(X509NameType.SimpleName, false), (cert, file.Name));
                    }
                    catch (Exception ex) {
                        _logger.LogError(ex, "Error in {method}: {file}", nameof(LoadCertificates), file.Name);
                    }
                }
            }
            foreach (var pfxPattern in pfxPatterns) {
                foreach (var file in _dirInfo.GetFiles(pfxPattern, SearchOption.TopDirectoryOnly)) {
                    try {
                        var cert = new X509Certificate2(file.FullName, (string?)null, X509KeyStorageFlags.PersistKeySet);
                        certs = certs.Add(cert.GetNameInfo(X509NameType.SimpleName, false), (cert, file.Name));
                    }
                    catch (Exception ex) {
                        _logger.LogError(ex, "Error in {method}: {file}", nameof(LoadCertificates), file.Name);
                    }
                }
            }
            Interlocked.MemoryBarrier();
            _certificates = certs;
        }

        // we should these types of results:
        // 1) Cert received and checked and installed OK - can remove file
        // 2) Agent not connected, call pending - leave file in place, retry later
        // 3) Some transient call error (e.g. network) - leave file in place, retry later
        // 4) Some terminal call error (e.g. data format) - failure, remove file
        // 5) Call successful, but cert invalid (terminal error) - failure, remove file
        // 6) Call successful, but cert not installed for some temporary reason (e.g. root cert missing) - leave file, retry later
        async Task<bool> CallAgentAsync(string agentId, X509Certificate2 cert, TimeSpan timeout) {
            if (_agentProxyMgr.TryGetProxy(agentId, out var proxy)) {
                var evt = new ControlEvent {
                    Id = proxy.GetNextEventId().ToString(),
                    Event = Constants.InstallCertEvent,
                    Data = cert.GetRawCertDataString()
                };
                var cts = new CancellationTokenSource(timeout);
                try {
                    var resultJson = await proxy.CallAsync(evt.Id, evt, cts.Token).ConfigureAwait(false);
                    if (resultJson != null) {
                        //TODO deserialize and check for errors
                    }
                    return true;
                }
                catch (Exception ex) {
                    _logger.LogError(ex, "Error in {method}", nameof(CallAgentAsync));
                    return false;
                }
            }
            return false;
        }

        void FileChangeError(object sender, ErrorEventArgs e) => _logger.LogError(e.GetException(), "Error in {class}", nameof(FileChangeDetector));

        // each type of change within the settle time is accumulated in the RenamedEventArgs argument!
        void FileChanged(object sender, RenamedEventArgs e) {
            // deletion (including renaming)
            var isDeletedOrRenamed = (e.OldName != null && e.Name != e.OldName)
                || ((e.ChangeType & WatcherChangeTypes.Deleted) == WatcherChangeTypes.Deleted && e.Name == null);
            if (isDeletedOrRenamed) {
                var deleted = _certificates
                    .Where(entry => string.Equals(entry.Value.Item2, e.OldName, StringComparison.CurrentCultureIgnoreCase))
                    .FirstOrDefault();
                if (!string.IsNullOrEmpty(deleted.Key)) {
                    ImmutableInterlocked.TryRemove(ref _certificates, deleted.Key, out _);
                }
            }
            // if the file still exists
            if (e.Name != null) {
                var newCert = LoadCertificate(e.FullPath);
                var newValue = (newCert, e.Name);
                var newKey = newCert.GetNameInfo(X509NameType.SimpleName, false);
                _ = ImmutableInterlocked.AddOrUpdate(ref _certificates, newKey, newValue, (k, v) => newValue);
            }
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken) {
            _stopRegistration = stoppingToken.Register(() => _fileChangeDetector.Stop());
            _fileChangeDetector.Start(true);
            return Task.CompletedTask;
        }

        public override void Dispose() {
            _stopRegistration?.Dispose();
            _fileChangeDetector.Dispose();
            base.Dispose();
        }
    }
}
