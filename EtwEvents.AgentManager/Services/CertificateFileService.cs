using Microsoft.Extensions.Options;

namespace KdSoft.EtwEvents.AgentManager
{
    public class CertificateFileService: BackgroundService
    {
        readonly DirectoryInfo _dirInfo;
        readonly IOptionsMonitor<AuthorizationOptions> _authOptsMonitor;
        readonly ILogger<CertificateFileService> _logger;

        public CertificateFileService(DirectoryInfo dirInfo, IOptionsMonitor<AuthorizationOptions> authOptsMonitor, ILogger<CertificateFileService> logger) {
            _dirInfo = dirInfo;
            _authOptsMonitor = authOptsMonitor;
            _logger = logger;
            // make sure directory exists
            _dirInfo.Create();

        }

        void CleanupPendingFiles() {
            var utcNow = DateTimeOffset.UtcNow;
            var files = _dirInfo.GetFiles();
            var fileExpiry = TimeSpan.FromDays(_authOptsMonitor.CurrentValue.PendingCertExpiryDays);
            foreach (var fi in files) {
                var fileAge = utcNow - fi.LastWriteTimeUtc;
                if (fileAge > fileExpiry) {
                    try {
                        fi.Delete();
                    }
                    catch (IOException ex) {
                        _logger.LogError(ex, "Error in {method}.", nameof(CleanupPendingFiles));
                    }
                }
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {

            while (!stoppingToken.IsCancellationRequested) {
                var checkPeriod = TimeSpan.FromMinutes(_authOptsMonitor.CurrentValue.PendingCertCheckMinutes);
                try {
                    CleanupPendingFiles();
                }
                catch (Exception ex) {
                    _logger.LogError(ex, "Error in {method}.", nameof(CertificateFileService));
                }
                await Task.Delay(checkPeriod, stoppingToken).ConfigureAwait(false);
            }
        }

        public async Task SaveAsync(IFormFile formFile, CancellationToken cancelToken) {
            var checkedName = Path.GetFileName(formFile.FileName);
            var filePath = Path.Combine(_dirInfo.FullName, checkedName);
            using (var fs = File.Create(filePath)) {
                await formFile.CopyToAsync(fs, cancelToken);
            }
        }
    }
}
