using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using KdSoft.EtwEvents.Client.Shared;

namespace KdSoft.EtwEvents.PushAgent
{
    class EventSinkService
    {
        readonly string _eventSinksDir;
        readonly string _rootPath;
        readonly string[] _runtimeAssemblyPaths;
        const string SinkAssemblyFilter = "*Sink.dll";

        public EventSinkService(string rootPath, string eventSinksDir) {
            this._rootPath = rootPath;
            this._eventSinksDir = eventSinksDir;
            this._runtimeAssemblyPaths = Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll");
        }

        public IEventSinkFactory? LoadEventSinkFactory(string sinkType, AssemblyLoadContext? loadContext = null) {
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
                            if (factoryTypeName != null) {
                                var factoryAssembly = loadContext.LoadFromAssemblyPath(evtSinkFile.FullName);
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
