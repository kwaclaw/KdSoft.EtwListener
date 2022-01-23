using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KdSoft.EtwEvents.PushAgent
{
    class EventSinkService
    {
        readonly string _rootPath;
        readonly string _eventSinksDir;
        readonly IOptions<ControlOptions> _controlOptions;
        readonly SocketsHttpHandler _httpHandler;
        readonly ILogger<EventSinkService> _logger;
        readonly string[] _runtimeAssemblyPaths;
        const string SinkAssemblyFilter = "*Sink.dll";

        public EventSinkService(
            string rootPath,
            string eventSinksDir,
            IOptions<ControlOptions> controlOptions,
            SocketsHttpHandler httpHandler,
            ILogger<EventSinkService> logger
        ) {
            this._rootPath = rootPath;
            this._eventSinksDir = eventSinksDir;
            this._controlOptions = controlOptions;
            this._httpHandler = httpHandler;
            this._logger = logger;
            this._runtimeAssemblyPaths = Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll");
        }

        /// <summary>
        /// Load event sink factory from its dedicated directory.
        /// Based on https://docs.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support.
        /// </summary>
        /// <param name="sinkType">Event sink type as indicated by the <see cref="EventSinkAttribute"/> of the event sink factory.</param>
        /// <param name="version">Version of event sink.</param>
        /// <returns>An <see cref="IEventSinkFactory"/> instance, and a reference to its <see cref="EventSinkLoadContext">assembly load context</see>.</returns>
        /// <remarks>It is ncessary to keep a reference to the sink factory's assembly load context, because
        /// it will be unloaded otherwise (as it is a collectible load context).</remarks>
        public (IEventSinkFactory? sinkFactory, EventSinkLoadContext? loadContext) LoadEventSinkFactory(string sinkType, string version) {
            var eventSinksDir = Path.Combine(_rootPath, _eventSinksDir);

            var dirInfo = new DirectoryInfo(eventSinksDir);
            var evtSinkDirectories = dirInfo.EnumerateDirectories();

            var assemblyPaths = new List<string>(_runtimeAssemblyPaths);
            assemblyPaths.Add(typeof(IEventSinkFactory).Assembly.Location);
            foreach (var evtSinkDir in evtSinkDirectories) {
                var evtSinkFile = evtSinkDir.GetFiles(SinkAssemblyFilter).FirstOrDefault();
                if (evtSinkFile != null) {
                    assemblyPaths.Add(evtSinkFile.FullName);
                }
            }

            // Create PathAssemblyResolver that can resolve assemblies using the created list.
            var resolver = new PathAssemblyResolver(assemblyPaths);
            using (var metaLoadContext = new MetadataLoadContext(resolver)) {
                foreach (var evtSinkDir in evtSinkDirectories) {
                    var evtSinkFile = evtSinkDir.GetFiles(SinkAssemblyFilter).FirstOrDefault();
                    if (evtSinkFile != null) {
                        var factoryTypes = metaLoadContext.GetEventSinkFactoriesBySinkType(evtSinkFile.FullName, sinkType);
                        foreach (var factoryType in factoryTypes) {
                            var factoryTypeName = factoryType.FullName;
                            var factoryAssemblyName = factoryType.Assembly.GetName();
                            // only interested in first one
                            if (factoryTypeName != null && factoryAssemblyName.Version?.ToString() == version) {
                                var loadContext = new EventSinkLoadContext(evtSinkFile.FullName);
                                var factoryAssembly = loadContext.LoadFromAssemblyName(factoryAssemblyName);
                                return ((IEventSinkFactory?)factoryAssembly.CreateInstance(factoryTypeName), loadContext);
                            }
                        }
                    }
                }
            }
            return (null, null);
        }

        public async Task<string> DownloadEventSink(string sinkType, string version) {
            var opts = _controlOptions.Value;
            var moduleUri = new Uri(opts.Uri, "Agent/GetEventSinkModule");
            var request = new HttpRequestMessage(HttpMethod.Post, moduleUri) {
                Content = JsonContent.Create(new { sinkType, version })
            };

            var dirName = $"{sinkType}~{version}";
            var eventSinkDir = Path.Combine(_rootPath, _eventSinksDir, dirName);

            _logger.LogInformation("Downloading event sink module '{dirName}' from {uri}", dirName, opts.Uri);

            var tempDir = Path.GetTempPath();
            var randFile = Path.GetRandomFileName();
            var zipTempFilename = Path.Combine(tempDir, randFile);

            try {
                using var http = new HttpClient(_httpHandler, false);
                using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                using var streamToReadFrom = await response.Content.ReadAsStreamAsync();
                using var streamToWriteTo = new FileStream(zipTempFilename, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);
                await streamToReadFrom.CopyToAsync(streamToWriteTo);

                streamToWriteTo.Position = 0;
                var archive = new ZipArchive(streamToWriteTo, ZipArchiveMode.Read);
                archive.ExtractToDirectory(eventSinkDir, true);
            }
            finally {
                if (zipTempFilename != null)
                    File.Delete(zipTempFilename);
            }

            return eventSinkDir;
        }
    }
}
