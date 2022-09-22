using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography.X509Certificates;
using KdSoft.Utils;

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

        /// <summary>
        /// Removes certificate from loaded certificates. Identity comparison based on thumbprint.
        /// Typically called when new certificate is first authorized: e.g. agent connects, agent sends state update.
        /// </summary>
        /// <param name="commonName">Subject CN of certificate to remove.</param>
        /// <param name="thumbprint">Thumbprint of certificate to remove.</param>
        /// <returns><c>true</c> if certificate was successfully removed, <c>false</c> otherwise.</returns>
        public bool TryRemoveCertificate(string commonName, string thumbprint) {
            (X509Certificate2, string) entry;
            if (_certificates.TryGetValue(commonName, out entry)) {
                if (entry.Item1.Thumbprint.ToLower() == thumbprint.ToLower()) {
                    var result = ImmutableInterlocked.TryRemove(ref _certificates, commonName, out entry);
                    if (result) {
                        try {
                            File.Delete(entry.Item2);
                        }
                        catch (Exception ex) {
                            _logger.LogError(ex, "Error in {method}: {file}", nameof(TryRemoveCertificate), entry.Item2);
                        }
                    }
                    return result;
                }
            }
            return false;
        }

        /// <summary>
        /// Prepares control event for push agent. Called when push agent connects.
        /// </summary>
        /// <param name="agentId"></param>
        /// <param name="eventId"></param>
        /// <returns></returns>
        public bool GetNewCertificate(string agentId, [NotNullWhen(true)] out X509Certificate2? cert) {
            if (_certificates.TryGetValue(agentId, out var certEntry)) {
                cert = certEntry.Item1;
            }
            else {
                cert = null;
            }
            return cert is not null;
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

        /// <summary>
        /// Tries to post new control event to pushagent, when push agent is connected.
        /// </summary>
        bool PostCertificateToAgent(string agentId, X509Certificate2 cert) {
            if (_agentProxyMgr.TryGetProxy(agentId, out var proxy)) {
                var certPEM = CertUtils.ExportToPEM(cert);
                certPEM = certPEM.ReplaceLineEndings("\ndata:");
                var evt = new ControlEvent {
                    Id = proxy.GetNextEventId().ToString(),
                    Event = Constants.InstallCertEvent,
                    Data = certPEM,
                };
                return proxy.Post(evt);
            }
            return false;
        }

        /// <summary>
        /// Tries to post new certificate to agent, otherwise saves the certificate for later when agent connects.
        /// </summary>
        void ProcessUpdatedCertificate(string certFilePath) {
            var newCert = CertUtils.LoadCertificate(certFilePath);
            var newKey = newCert.GetNameInfo(X509NameType.SimpleName, false);
            // track the certificate so we can remove the file once we get a successful update from the agent
            var newValue = (newCert, certFilePath);
            _ = ImmutableInterlocked.AddOrUpdate(ref _certificates, newKey, newValue, (k, v) => newValue);
            var posted = PostCertificateToAgent(newKey, newCert);
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
                ProcessUpdatedCertificate(e.FullPath);
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
