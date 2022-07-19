using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
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
        readonly ILogger<AgentCertificateWatcher> _logger;
        readonly FileChangeDetector _fileChangeDetector;

        readonly ImmutableArray<string> pemPatterns = ImmutableArray.Create<string>("*.pem", "*.crt", "*.cer");
        readonly ImmutableArray<string> pfxPatterns = ImmutableArray.Create<string>("*.pfx", "*.p12");

        CancellationTokenRegistration? _stopRegistration;

        ImmutableDictionary<string, (X509Certificate2, string)> _certificates;
        public ImmutableDictionary<string, (X509Certificate2, string)> Certificates => _certificates;

        public readonly TimeSpan SettleTime = TimeSpan.FromSeconds(5);

        public AgentCertificateWatcher(DirectoryInfo dirInfo, ILogger<AgentCertificateWatcher> logger) {
            this._dirInfo = dirInfo;
            this._logger = logger;
            var nf = NotifyFilters.LastWrite | NotifyFilters.FileName;
            var searchPatterns = pemPatterns.AddRange(pfxPatterns);
            _fileChangeDetector = new FileChangeDetector(dirInfo.FullName, searchPatterns, false, nf, SettleTime);
            _fileChangeDetector.FileChanged += FileChanged;
            _fileChangeDetector.ErrorEvent += FileChangeError;
            _certificates = ImmutableDictionary<string, (X509Certificate2, string)>.Empty;
        }

        public X509Certificate2 LoadCertificate(string filePath) {
            var ext = Path.GetExtension(filePath).ToLower();
            switch (ext) {
                case ".pem":
                case ".crt":
                case ".cer":
                    return X509Certificate2.CreateFromPemFile(filePath);
                case ".pfx":
                case ".p12":
                    return new X509Certificate2(filePath, (string?)null, X509KeyStorageFlags.PersistKeySet);
                default:
                    throw new ArgumentException($"Unregognized certificate file extension: {ext}", nameof(filePath));
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

        void FileChangeError(object sender, ErrorEventArgs e) => _logger.LogError(e.GetException(), "Error in {class}", nameof(FileChangeDetector));

        // each type of change within the settle time is accumulated in the RenamedEventArgs argument!
        void FileChanged(object sender, RenamedEventArgs e) {
            // deletion (including renaming)
            var isDeleted = (e.OldName != null && e.Name != e.OldName)
                || ((e.ChangeType & WatcherChangeTypes.Deleted) == WatcherChangeTypes.Deleted && e.Name == null);
            if (isDeleted) {
                var deleted = _certificates
                    .Where(entry => string.Equals(entry.Value.Item2, e.OldName, StringComparison.CurrentCultureIgnoreCase))
                    .FirstOrDefault();
                if (!string.IsNullOrEmpty(deleted.Key)) {
                    ImmutableInterlocked.TryRemove(ref _certificates, deleted.Key, out _);
                }
            }
            // any other change
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
