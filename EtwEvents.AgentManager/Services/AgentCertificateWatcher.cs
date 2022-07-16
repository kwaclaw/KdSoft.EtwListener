using System;
using System.Collections.Immutable;
using System.IO;
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

        ImmutableDictionary<string, X509Certificate2> _certificates;
        public ImmutableDictionary<string, X509Certificate2> Certificates => _certificates;

        public readonly TimeSpan SettleTime = TimeSpan.FromSeconds(5);

        public AgentCertificateWatcher(DirectoryInfo dirInfo, ILogger<AgentCertificateWatcher> logger) {
            this._dirInfo = dirInfo;
            this._logger = logger;
            var nf = NotifyFilters.LastWrite | NotifyFilters.FileName;
            var searchPatterns = pemPatterns.AddRange(pfxPatterns);
            _fileChangeDetector = new FileChangeDetector(dirInfo.FullName, searchPatterns, false, nf, SettleTime);
            _fileChangeDetector.FileChanged += FileChanged;
            _fileChangeDetector.Error += FileChangeError;
            _certificates = ImmutableDictionary<string, X509Certificate2>.Empty;
        }

        /// <summary>
        /// Loads certificates from directory. Certificate files must not be password protected.
        /// </summary>
        public void LoadCertificates() {
            var certs = ImmutableDictionary<string, X509Certificate2>.Empty;
            foreach (var pemPattern in pemPatterns) {
                foreach (var file in _dirInfo.GetFiles(pemPattern, SearchOption.TopDirectoryOnly)) {
                    try {
                        var cert = X509Certificate2.CreateFromPemFile(file.FullName);
                        certs = certs.Add(cert.GetNameInfo(X509NameType.SimpleName, false), cert);
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
                        certs = certs.Add(cert.GetNameInfo(X509NameType.SimpleName, false), cert);
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

        void FileChanged(object sender, FileSystemEventArgs e) {
            LoadCertificates();
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
