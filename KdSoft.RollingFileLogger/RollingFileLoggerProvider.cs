using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KdSoft.Logging {
    [ProviderAlias("RollingFile")]  // name for this provider's settings in the Logging section of appsettings.json
    public class RollingFileLoggerProvider: ILoggerProvider, IAsyncDisposable {
        readonly RollingFileFactory _fileFactory;
        readonly IOptions<RollingFileLoggerOptions> _options;

        //TODO Look at https://github.com/adams85/filelogger/blob/master/source/FileLogger/FileLoggerProvider.cs
        //TODO Look at https://github.com/aspnet/Logging/blob/master/src/Microsoft.Extensions.Logging.EventSource/EventSourceLoggerFactoryExtensions.cs

        public RollingFileLoggerProvider(IOptions<RollingFileLoggerOptions> options) {
            this._options = options;
            var opts = options.Value;

            Func<DateTimeOffset, string> fileNameSelector = (dto) => string.Format(opts.FileNameFormat, dto);
            var dirInfo = new DirectoryInfo(opts.Directory);
            dirInfo.Create();

            _fileFactory = new RollingFileFactory(
                dirInfo,
                fileNameSelector,
                opts.FileExtension,
                opts.UseLocalTime,
                opts.FileSizeLimitKB,
                opts.MaxFileCount,
                opts.NewFileOnStartup
            );
        }

        public ILogger CreateLogger(string categoryName) {
            var opts = _options.Value;
            return new RollingFileLogger(_fileFactory, categoryName, LogLevel.Trace, opts.BatchSize, opts.MaxWriteDelayMSecs);
        }

        public void Dispose() => _fileFactory?.Dispose();

        public ValueTask DisposeAsync() => _fileFactory.DisposeAsync();
    }
}
