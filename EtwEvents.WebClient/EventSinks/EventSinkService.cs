using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
        const string SinkAssemblyFilter = "*Sink.dll";

        public EventSinkService(TraceSessionManager sessionManager, IHostEnvironment env, IStringLocalizer<EventSinkService> localize) {
            this._sessionManager = sessionManager;
            this._env = env;
            this._ = localize;
        }

        /// <summary>
        /// Returns event sink types in configured container directory.
        /// The subdirectory name defines the event sink type.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<EventSinkInfo> GetEventSinkTypes() {
            var eventSinksDir = Path.Combine(_env.ContentRootPath, "EventSinks");
            var dirInfo = new DirectoryInfo(eventSinksDir);
            var evtSinkDirectories = dirInfo.EnumerateDirectories();

            foreach (var evtSinkDir in evtSinkDirectories) {
                var evtSinkFile = evtSinkDir.GetFiles(SinkAssemblyFilter).FirstOrDefault();
                if (evtSinkFile != null) {
                    var evtSinkAssembly = Assembly.ReflectionOnlyLoadFrom(evtSinkFile.FullName);
                    var evtSinkFactories = evtSinkAssembly.GetEventSinkFactories();
                    foreach (var evtSinkFactory in evtSinkFactories) {
                        var sinkType = evtSinkFactory.GetEventSinkType();
                        if (sinkType != null) {
                            yield return new EventSinkInfo { SinkType = sinkType, Description = _.GetString(sinkType) };
                            break;  // only interested in first one
                        }
                    }
                }
            }
        }

        public IEventSinkFactory? LoadEventSinkFactory(string sinkType) {
            var eventSinksDir = Path.Combine(_env.ContentRootPath, "EventSinks");
            var dirInfo = new DirectoryInfo(eventSinksDir);
            var evtSinkDirectories = dirInfo.EnumerateDirectories();

            foreach (var evtSinkDir in evtSinkDirectories) {
                var evtSinkFile = evtSinkDir.GetFiles(SinkAssemblyFilter).FirstOrDefault();
                if (evtSinkFile != null) {
                    var evtSinkAssembly = Assembly.ReflectionOnlyLoadFrom(evtSinkFile.FullName);
                    var factoryTypes = evtSinkAssembly.GetEventSinkFactoriesBySinkType(sinkType);
                    foreach (var factoryType in factoryTypes) {
                        var factoryAssembly = Assembly.LoadFrom(evtSinkFile.FullName);
                        var factoryTypeName = factoryType.FullName;
                        // only interested in first one
                        if (factoryTypeName != null)
                            return (IEventSinkFactory?)factoryAssembly.CreateInstance(factoryTypeName);
                    }
                }
            }
            return null;
        }
    }
}
