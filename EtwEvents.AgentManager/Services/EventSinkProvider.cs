using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using KdSoft.EtwEvents.Client;
using KdSoft.EtwLogging;
using Microsoft.Extensions.Logging;

namespace KdSoft.EtwEvents.AgentManager
{
    class EventSinkProvider
    {
        readonly string _rootPath;
        readonly string _eventSinksDirName;
        readonly string _eventSinksCacheDirName;
        readonly string[] _runtimeAssemblyPaths;
        const string SinkAssemblyFilter = "*Sink.dll";

        public EventSinkProvider(string rootPath, string eventSinksDirName, string eventSinksCacheDirName) {
            this._rootPath = rootPath;
            this._eventSinksDirName = eventSinksDirName;
            this._eventSinksCacheDirName = eventSinksCacheDirName;
            this._runtimeAssemblyPaths = Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll");
        }

        public IEnumerable<(EventSinkInfo, DirectoryInfo?)> GetEventSinkInfos(DirectoryInfo evtSinkDirInfo) {
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
                    var (evtSinkType, evtSinkVersion) = metaLoadContext.GetEventSinkTypes(evtSinkFile.FullName).FirstOrDefault();
                    if (evtSinkType != null) {
                        var version = evtSinkVersion ?? "0.0";
                        var sinkInfo = new EventSinkInfo {
                            SinkType = evtSinkType,
                            Version = version,
                            //TODO (future) CredentialsSchema = ?
                            //TODO (future) OptionsSchema = ?
                        };
                        yield return (sinkInfo, evtSinkDirInfo);
                    }
                }
            }
        }

        /// <summary>
        /// Returns event sink types in configured container directory.
        /// The subdirectory name must match the event sink type.
        /// </summary>
        public IEnumerable<(EventSinkInfo, DirectoryInfo?)> GetEventSinkInfos() {
            var eventSinksDir = Path.Combine(_rootPath, _eventSinksDirName);
            var eventSinksDirInfo = new DirectoryInfo(eventSinksDir);
            var evtSinkDirectories = eventSinksDirInfo.EnumerateDirectories();

            foreach (var evtSinkDirInfo in evtSinkDirectories) {
                var dirSinkInfos = GetEventSinkInfos(evtSinkDirInfo);
                foreach (var (sinkInfo, dirInfo) in dirSinkInfos) {
                    yield return (sinkInfo, dirInfo);
                }
            }
        }

        string GetEventSinkZipFileName(string sinkType, string version) {
            return $"{sinkType}~{version}.zip";
        }

        string GetFullEventSinkZipFileName(string sinkType, string version) {
            var zipFileName = GetEventSinkZipFileName(sinkType, version);
            // create cache directory if it does not exist
            var cacheDir = Directory.CreateDirectory(Path.Combine(_rootPath, _eventSinksCacheDirName));
            return Path.Combine(cacheDir.FullName, zipFileName);
        }

        bool CreateEventSinkZipFile(string sinkType, string version, string zipFileName) {
            var sinkInfos = GetEventSinkInfos();
            var matching = sinkInfos.Where(si =>
                string.Equals(si.Item1.SinkType, sinkType, StringComparison.CurrentCultureIgnoreCase) && si.Item1.Version == version);
            if (!matching.Any())
                return false;
            var sinkDir = matching.First().Item2;
            if (sinkDir == null)
                return false;

            ZipFile.CreateFromDirectory(sinkDir.FullName, zipFileName);
            return true;
        }

        public string? GetEventSinkZipFile(string sinkType, string version, bool create) {
            var zipFileName = GetFullEventSinkZipFileName(sinkType, version);
            if (File.Exists(zipFileName))
                return zipFileName;
            if (!create)
                return null;

            var tempDir = Path.GetTempPath();
            var randFile = Path.GetRandomFileName();
            var zipTempFilename = Path.Combine(tempDir, randFile);
            try {
                if (!CreateEventSinkZipFile(sinkType, version, zipTempFilename))
                    return null;
                // this operation is usually atomic when on the same drive
                File.Move(zipTempFilename, zipFileName, true);
                return zipFileName;
            }
            finally {
                File.Delete(zipTempFilename);
            }
        }
    }
}
