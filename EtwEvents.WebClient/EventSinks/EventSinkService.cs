using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using KdSoft.EtwEvents.Client.Shared;
using KdSoft.EtwEvents.WebClient.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;

namespace KdSoft.EtwEvents.WebClient.EventSinks
{
    class EventSinkService
    {
        readonly TraceSessionManager _sessionManager;
        readonly IHostEnvironment _env;
        readonly IStringLocalizer<EventSinkService> _;
        readonly string[] _runtimeAssemblyPaths;
        const string SinkAssemblyFilter = "*Sink.dll";

        public EventSinkService(TraceSessionManager sessionManager, IHostEnvironment env, IStringLocalizer<EventSinkService> localize) {
            this._sessionManager = sessionManager;
            this._env = env;
            this._ = localize;
            this._runtimeAssemblyPaths = Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll");
        }

        /// <summary>
        /// Returns event sink types in configured container directory.
        /// The subdirectory name defines the event sink type.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<EventSinkInfo> GetEventSinkInfos() {
            var eventSinksDir = Path.Combine(_env.ContentRootPath, "EventSinks");
            var eventSinksDirInfo = new DirectoryInfo(eventSinksDir);
            // trailing '/' is important for building relative Uris
            var eventSinksDirUri = new Uri($"file:///{eventSinksDirInfo.FullName}/");
            var evtSinkDirectories = eventSinksDirInfo.EnumerateDirectories();

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
                        var evtSinkType = metaLoadContext.GetEventSinkTypes(evtSinkFile.FullName).FirstOrDefault();
                        if (evtSinkType != null) {
                            var configView = evtSinkDir.GetFiles(@"config/*-config.js").First();
                            var configViewUri = new Uri($"file:///{configView.FullName}");
                            var configModel = evtSinkDir.GetFiles(@"config/*-config-model.js").First();
                            var configModelUri = new Uri($"file:///{configModel.FullName}");
                            yield return new EventSinkInfo {
                                SinkType = evtSinkType,
                                Description = _.GetString(evtSinkType),
                                // relative Uri does not include "EventSinks" path component (has a trailing '/')
                                ConfigViewUrl = eventSinksDirUri.MakeRelativeUri(configViewUri),
                                ConfigModelUrl = eventSinksDirUri.MakeRelativeUri(configModelUri),
                            };
                        }
                    }
                }
            }
        }

        public IEventSinkFactory? LoadEventSinkFactory(string sinkType) {
            var eventSinksDir = Path.Combine(_env.ContentRootPath, "EventSinks");
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
                        var factoryTypes = metaLoadContext.GetEventSinkFactoriesBySinkType(evtSinkFile.FullName, sinkType, out var _);
                        foreach (var factoryType in factoryTypes) {
                            var factoryTypeName = factoryType.FullName;
                            // only interested in first one
                            if (factoryTypeName != null) {
                                var factoryAssembly = Assembly.LoadFrom(evtSinkFile.FullName);
                                return (IEventSinkFactory?)factoryAssembly.CreateInstance(factoryTypeName);
                            }
                        }
                    }
                }
            }
            return null;
        }
    }
}
