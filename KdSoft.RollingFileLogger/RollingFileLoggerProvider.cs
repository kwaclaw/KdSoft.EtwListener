using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KdSoft.Logging {
    [ProviderAlias("RollingFile")]  // name for this provider's settings in the Logging section of appsettings.json
    public class RollingFileLoggerProvider: ILoggerProvider, IAsyncDisposable {
        readonly RollingFileFactory _fileFactory;

        //TODO Look at https://github.com/adams85/filelogger/blob/master/source/FileLogger/FileLoggerProvider.cs
        //TODO Look at https://github.com/aspnet/Logging/blob/master/src/Microsoft.Extensions.Logging.EventSource/EventSourceLoggerFactoryExtensions.cs

        public RollingFileLoggerProvider(IOptions<RollingFileLoggerOptions> options) {
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
            return new RollingFileLogger(_fileFactory, categoryName, LogLevel.Trace);
        }

        public void Dispose() => _fileFactory?.Dispose();

        public ValueTask DisposeAsync() => _fileFactory.DisposeAsync();
    }
}
