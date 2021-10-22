using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using KdSoft.EtwEvents.Client.Shared;

namespace KdSoft.EtwEvents.AgentManager.Services
{
    class EventSinkService
    {
        readonly string _rootPath;
        readonly string _eventSinksDirName;
        readonly string _eventSinksCacheDirName;
        readonly string _eventSinksConfigDirName;
        readonly string[] _runtimeAssemblyPaths;
        const string SinkAssemblyFilter = "*Sink.dll";

        public EventSinkService(string rootPath, string eventSinksDirName, string eventSinksCacheDirName, string eventSinksConfigDirName) {
            this._rootPath = rootPath;
            this._eventSinksDirName = eventSinksDirName;
            this._eventSinksCacheDirName = eventSinksCacheDirName;
            this._eventSinksConfigDirName = eventSinksConfigDirName;
            this._runtimeAssemblyPaths = Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll");
        }

        /// <summary>
        /// Returns event sink types in configured container directory.
        /// The subdirectory name must match the event sink type.
        /// </summary>
        public IEnumerable<(EventSinkInfo, DirectoryInfo?)> GetEventSinkInfos() {
            var eventSinksDir = Path.Combine(_rootPath, _eventSinksDirName);
            var eventSinksConfigDir = Path.Combine(_rootPath, _eventSinksConfigDirName);

            var eventSinksDirInfo = new DirectoryInfo(eventSinksDir);
            var evtSinkDirectories = eventSinksDirInfo.EnumerateDirectories();

            var eventSinksConfigDirInfo = new DirectoryInfo(eventSinksConfigDir);
            // trailing '/' is important for building relative Uris
            var eventSinksConfigDirUri = new Uri($"file:///{eventSinksConfigDirInfo.FullName}/");

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
                        var (evtSinkType, evtSinkVersion) = metaLoadContext.GetEventSinkTypes(evtSinkFile.FullName).FirstOrDefault();
                        if (evtSinkType != null) {
                            var version = evtSinkVersion ?? "0.0";
                            var sinkRelativeDir = Path.GetRelativePath(eventSinksDir, evtSinkDir.FullName);
                            var configView = eventSinksConfigDirInfo.GetFiles(@$"{sinkRelativeDir}/*-config.js").First();
                            var configViewUri = new Uri($"file:///{configView.FullName}");
                            var configModel = eventSinksConfigDirInfo.GetFiles(@$"{sinkRelativeDir}/*-config-model.js").First();
                            var configModelUri = new Uri($"file:///{configModel.FullName}");
                            var sinkInfo = new EventSinkInfo {
                                SinkType = evtSinkType,
                                Version = version,
                                // relative Uri does not include "EventSinks" path component (has a trailing '/')
                                ConfigViewUrl = eventSinksConfigDirUri.MakeRelativeUri(configViewUri),
                                ConfigModelUrl = eventSinksConfigDirUri.MakeRelativeUri(configModelUri),
                            };
                            yield return (sinkInfo, evtSinkDir);
                        }
                    }
                }
            }
        }

        string GetEventSinkZipFileName(string sinkType, string version) {
            return $"{sinkType}~{version}.zip";
        }

        string GetFullEventSinkZipFileName(string sinkType, string version) {
            var zipFileName = GetEventSinkZipFileName(sinkType, version);
            return Path.Combine(_rootPath, _eventSinksCacheDirName, zipFileName);
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
                // this operation is atomic, it should also work when zipFileName is open with FileShare.Delete
                File.Replace(zipTempFilename, zipFileName, null);
                return zipFileName;
            }
            finally {
                File.Delete(zipTempFilename);
            }
        }
    }
}
