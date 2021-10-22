using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Threading.Tasks;
using KdSoft.EtwEvents.Client.Shared;
using Microsoft.Extensions.Options;

namespace KdSoft.EtwEvents.PushAgent
{
    class EventSinkService
    {
        readonly string _rootPath;
        readonly string _eventSinksDir;
        readonly IOptions<ControlOptions> _controlOptions;
        readonly HttpClient _http;
        readonly string[] _runtimeAssemblyPaths;
        const string SinkAssemblyFilter = "*Sink.dll";

        public EventSinkService(string rootPath, string eventSinksDir, IOptions<ControlOptions> controlOptions, HttpClient http) {
            this._rootPath = rootPath;
            this._eventSinksDir = eventSinksDir;
            this._controlOptions = controlOptions;
            this._http = http;
            this._runtimeAssemblyPaths = Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll");
        }

        public IEventSinkFactory? LoadEventSinkFactory(string sinkType, string version, AssemblyLoadContext? loadContext = null) {
            if (loadContext is null) {
                loadContext = AssemblyLoadContext.Default;
            }

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
                            // only interested in first one
                            if (factoryTypeName != null && factoryType.Assembly.GetName().Version?.ToString() == version) {
                                var factoryAssembly = loadContext.LoadFromAssemblyPath(evtSinkFile.FullName);
                                return (IEventSinkFactory?)factoryAssembly.CreateInstance(factoryTypeName);
                            }
                        }
                    }
                }
            }
            return null;
        }
        public async Task<string> DownloadEventSink(string sinkType, string version) {
            var opts = _controlOptions.Value;
            var moduleUri = new Uri(opts.Uri, "Agent/GetEventSinkModule");
            var request = new HttpRequestMessage(HttpMethod.Post, moduleUri);
            request.Content = JsonContent.Create(new { sinkType, version });

            var dirName = $"{sinkType}~{ version}";
            var eventSinkDir = Path.Combine(_rootPath, _eventSinksDir, dirName);

            var tempDir = Path.GetTempPath();
            var randFile = Path.GetRandomFileName();
            var zipTempFilename = Path.Combine(tempDir, randFile);
            try {
                using (var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead)) {
                    response.EnsureSuccessStatusCode();
                    using (var streamToReadFrom = await response.Content.ReadAsStreamAsync()) {
                        using (var streamToWriteTo = new FileStream(zipTempFilename, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose)) {
                            await streamToReadFrom.CopyToAsync(streamToWriteTo);
                            streamToWriteTo.Position = 0;
                            var archive = new ZipArchive(streamToWriteTo, ZipArchiveMode.Read);
                            archive.ExtractToDirectory(eventSinkDir, true);
                        }
                    }
                }
            }
            finally {
                if (zipTempFilename != null)
                    File.Delete(zipTempFilename);
            }

            return eventSinkDir;
        }
    }

}
