using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using KdSoft.EtwEvents.Client.Shared;
using Microsoft.Extensions.Hosting;

namespace KdSoft.EtwEvents.PushAgent
{
    class EventSinkService
    {
        readonly IHostEnvironment _env;
        readonly string[] _runtimeAssemblyPaths;
        const string SinkAssemblyFilter = "*Sink.dll";

        public EventSinkService(IHostEnvironment env) {
            this._env = env;
            this._runtimeAssemblyPaths = Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll");
        }

        public IEventSinkFactory? LoadEventSinkFactory(string sinkType, AssemblyLoadContext? loadContext = null) {
            if (loadContext is null) {
                loadContext = AssemblyLoadContext.Default;
            }

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
