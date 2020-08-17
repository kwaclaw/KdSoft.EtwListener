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
        readonly IStringLocalizer<EventSinkController> _;

        public EventSinkService(TraceSessionManager sessionManager, IHostEnvironment env, IStringLocalizer<EventSinkController> localize) {
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
                var evtSinkFile = evtSinkDir.GetFiles($"*.Sink.dll").FirstOrDefault();
                if (evtSinkFile != null) {
                    var evtSinkAssembly = Assembly.ReflectionOnlyLoadFrom(evtSinkFile.FullName);
                    var evtSinkInfos = evtSinkAssembly.GetEventSinkFactories();
                    foreach (var evtSinkInfo in evtSinkInfos) {
                        yield return new EventSinkInfo { SinkType = evtSinkInfo.sinkType, Description = _.GetString(evtSinkInfo.sinkType) };
                        break;  // only interested in first one
                    }
                }
            }
        }

        public IEventSinkFactory? LoadEventSinkFactory(string sinkType) {
            var eventSinksDir = Path.Combine(_env.ContentRootPath, "EventSinks");
            var dirInfo = new DirectoryInfo(eventSinksDir);
            var evtSinkDirectories = dirInfo.EnumerateDirectories();

            foreach (var evtSinkDir in evtSinkDirectories) {
                var evtSinkFile = evtSinkDir.GetFiles($"*.Sink.dll").FirstOrDefault();
                if (evtSinkFile != null) {
                    var evtSinkAssembly = Assembly.ReflectionOnlyLoadFrom(evtSinkFile.FullName);
                    var factoryTypes = evtSinkAssembly.GetEventSinkFactoriesBySinkType(sinkType);
                    foreach (var factoryType in factoryTypes) {
                        var factoryAssembly = Assembly.LoadFrom(evtSinkFile.FullName);
                        // only interested in first one
                        return (IEventSinkFactory?)factoryAssembly.CreateInstance(factoryType.FullName);
                    }
                }
            }
            return null;
        }
    }
}
