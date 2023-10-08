using System.IO.Compression;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.InteropServices;
using KdSoft.EtwLogging;
using Microsoft.Extensions.Options;

namespace KdSoft.EtwEvents.PushAgent
{
    class EventSinkService
    {
        readonly string _rootPath;
        readonly string _eventSinksDir;
        readonly IOptions<ControlOptions> _controlOptions;
        readonly SocketsHandlerCache _httpHandlerCache;
        readonly ILogger<EventSinkService> _logger;
        readonly string[] _runtimeAssemblyPaths;
        const string SinkAssemblyFilter = "*Sink.dll";

        public EventSinkService(
            string rootPath,
            string eventSinksDir,
            IOptions<ControlOptions> controlOptions,
            SocketsHandlerCache httpHandlerCache,
            ILogger<EventSinkService> logger
        ) {
            this._rootPath = rootPath;
            this._eventSinksDir = eventSinksDir;
            this._controlOptions = controlOptions;
            this._httpHandlerCache = httpHandlerCache;
            this._logger = logger;
            this._runtimeAssemblyPaths = Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll");
        }

#pragma warning disable CA1806 // Do not ignore method results
        static EventSinkService() {
            // Shared assemblies should be loaded before requested by the EventSinkLoadContext, because
            // otherwise they would be loaded using default probing which requires the version to match.
            new EventSinkException(); // (for IEventSinkFactory)
            var _ = global::Google.Protobuf.ByteString.Empty;
            new RetryStatus(); // (for IEventFilter)
            new EtwEventBatch();
            // more? ILogger should get loaded in the contructor
        }
#pragma warning restore CA1806 // Do not ignore method results

        public Type? GetEventSinkFactoryType(DirectoryInfo evtSinkDirInfo, string sinkType, string version) {
            var assemblyPaths = new HashSet<string>(_runtimeAssemblyPaths, StringComparer.CurrentCultureIgnoreCase);
            // see https://docs.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support 
            // we add these explicitly, as we have them loaded locally, and the event sink should not include them
            assemblyPaths.Add(typeof(IEventSinkFactory).Assembly.Location);
            assemblyPaths.Add(typeof(global::Google.Protobuf.MessageParser).Assembly.Location);
            assemblyPaths.Add(typeof(EtwEventBatch).Assembly.Location);
            assemblyPaths.Add(typeof(IEventFilter).Assembly.Location);
            assemblyPaths.Add(typeof(ILogger).Assembly.Location);
            var evtSinkFiles = evtSinkDirInfo.GetFiles("*.dll");
            foreach (var evtSinkFile in evtSinkFiles) {
                assemblyPaths.Add(evtSinkFile.FullName);
            }

            // Create PathAssemblyResolver that can resolve assemblies using the created list.
            var resolver = new PathAssemblyResolver(assemblyPaths);
            using (var metaLoadContext = new MetadataLoadContext(resolver)) {
                var evtSinkFile = evtSinkDirInfo.GetFiles(SinkAssemblyFilter).FirstOrDefault();
                if (evtSinkFile != null) {
                    var factoryTypes = metaLoadContext.GetEventSinkFactoriesBySinkType(evtSinkFile.FullName, sinkType);
                    foreach (var factoryType in factoryTypes) {
                        var factoryTypeName = factoryType.FullName;
                        var factoryAssemblyName = factoryType.Assembly.GetName();
                        // only interested in first one
                        if (factoryTypeName != null && factoryAssemblyName.Version?.ToString() == version) {
                            return factoryType;
                        }
                    }
                }
            }
            return null;
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

            Type? factoryMetaType = default;
            foreach (var evtSinkDirInfo in evtSinkDirectories) {
                factoryMetaType = GetEventSinkFactoryType(evtSinkDirInfo, sinkType, version);
                if (factoryMetaType != null)
                    break;
            }

            if (factoryMetaType?.FullName != null) {
                // make sure we do not let the EventSinkLoadContext load shared assemblies,
                // as we would get TypeLoadExceptions like "method XXX has no implementation"
                var loadContext = new EventSinkLoadContext(
                    factoryMetaType.Assembly.Location
                    // these are the shared assemblies' names
                    , typeof(IEventSinkFactory).Assembly.GetName()
                    , typeof(global::Google.Protobuf.MessageParser).Assembly.GetName()
                    , typeof(EtwEventBatch).Assembly.GetName()
                    , typeof(IEventFilter).Assembly.GetName()
                    , typeof(ILogger).Assembly.GetName()
                );
                var factoryAssembly = loadContext.LoadFromAssemblyName(factoryMetaType.Assembly.GetName());
                var factory = factoryAssembly.CreateInstance(factoryMetaType.FullName);
                return ((IEventSinkFactory?)factory, loadContext);
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
                using var http = new HttpClient(_httpHandlerCache.Handler, false);
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
