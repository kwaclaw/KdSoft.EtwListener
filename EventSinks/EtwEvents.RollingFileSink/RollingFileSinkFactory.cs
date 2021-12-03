using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using KdSoft.EtwEvents.Client;
using KdSoft.Utils;
using Microsoft.Extensions.Logging;

namespace KdSoft.EtwEvents.EventSinks
{
    [EventSink(nameof(RollingFileSink))]
    public class RollingFileSinkFactory: IEventSinkFactory
    {
        static readonly JsonSerializerOptions _serializerOptions;
        static readonly string _evtSinkDir;

        static RollingFileSinkFactory() {
            var evtSinkAssembly = Assembly.GetExecutingAssembly();
            _evtSinkDir = Path.GetDirectoryName(evtSinkAssembly.Location)!;
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) => Client.Utils.DirectoryResolveAssembly(_evtSinkDir, args);

            _serializerOptions = new JsonSerializerOptions {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public Task<IEventSink> Create(RollingFileSinkOptions options, ILogger logger) {
            try {
                Func<DateTimeOffset, string> fileNameSelector = (dto) => string.Format(options.FileNameFormat, dto);
                // returns options.Directory if it is an absolute path
                var absoluteDirectory = Path.Combine(_evtSinkDir, options.Directory);
                var dirInfo = new DirectoryInfo(absoluteDirectory);
                dirInfo.Create();

                var rollingFileFactory = new RollingFileFactory(
                    dirInfo,
                    fileNameSelector,
                    options.FileExtension,
                    options.UseLocalTime,
                    options.FileSizeLimitKB,
                    options.MaxFileCount,
                    options.NewFileOnStartup
                );
                return Task.FromResult((IEventSink)new RollingFileSink(rollingFileFactory, logger));
            }
            catch (Exception ex) {
                logger.LogError(ex, "Error in {eventSink} initialization.", nameof(RollingFileSink));
                throw;
            }
        }

        public Task<IEventSink> Create(string optionsJson, string credentialsJson, ILogger logger) {
            var options = JsonSerializer.Deserialize<RollingFileSinkOptions>(optionsJson, _serializerOptions);
            //var creds = JsonSerializer.Deserialize<RollingFileSinkCredentials>(credentialsJson, _serializerOptions);
            return Create(options!, logger);
        }

        public string GetCredentialsJsonSchema() {
            throw new NotImplementedException();
        }

        public string GetOptionsJsonSchema() {
            throw new NotImplementedException();
        }
    }
}
