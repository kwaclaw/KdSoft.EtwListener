using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KdSoft.EtwEvents.AgentManager
{
    public class FileCleanupService: BackgroundService
    {
        readonly DirectoryInfo _dirInfo;
        readonly TimeSpan _fileExpiry;
        readonly TimeSpan _checkPeriod;
        readonly ILogger<FileCleanupService> _logger;

        public FileCleanupService(DirectoryInfo dirInfo, TimeSpan fileExpiry, TimeSpan checkPeriod, ILogger<FileCleanupService> logger) {
            _dirInfo = dirInfo;
            _fileExpiry = fileExpiry;
            _checkPeriod = checkPeriod;
            _logger = logger;
            // make sure directory exists
            _dirInfo.Create();

        }

        void CleanupPendingFiles() {
            var utcNow = DateTimeOffset.UtcNow;
            var files = _dirInfo.GetFiles();
            foreach (var fi in files) {
                var fileAge = utcNow - fi.LastWriteTimeUtc;
                if (fileAge > _fileExpiry) {
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
                try {
                    CleanupPendingFiles();
                }
                catch (Exception ex) {
                    _logger.LogError(ex, "Error in {method}.", nameof(FileCleanupService));
                }
                await Task.Delay(_checkPeriod, stoppingToken).ConfigureAwait(false);
            }
        }
    }
}
