using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using KdSoft.EtwLogging;
using KdSoft.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace EtwEvents.WebClient
{
    class TraceSessionManager: ConcurrentTimedLifeCycleManager<string, TraceSessionEntry>
    {
        readonly IServiceProvider _services;
        readonly ILoggerFactory _loggerFactory;
        readonly TimeSpan _sessionIdleTime;
        readonly IStringLocalizer<TraceSession> _localizer;


        public TraceSessionManager(
            IConfiguration config,
            IServiceProvider services,
            ILoggerFactory loggerFactory,
            IStringLocalizer<TraceSession> localizer
        ) : base(TimeSpan.TryParse(config?["ReapPeriod"], out var reapPeriod) ? reapPeriod : TimeSpan.FromMinutes(5))
        {
            this._services = services;
            this._loggerFactory = loggerFactory;
            this._localizer = localizer;
            if (!TimeSpan.TryParse(config?["SessionIdleTime"], out this._sessionIdleTime))
                this._sessionIdleTime = TimeSpan.FromMinutes(5);
        }

        public Task<TraceSession> OpenSession(
            string name,
            string host,
            X509Certificate2 clientCertificate,
            IReadOnlyList<ProviderSetting> providers,
            Duration lifeTime
        ) {
            // NOTE: the value factory might be executed multiple times with this overload of GetOrAdd,
            //       if this is a problem then we need to use Lazy<T> instead.
            var entry = this.GetOrAdd(name, sessionName => {
                var sessionLogger = _loggerFactory.CreateLogger<TraceSession>();
                var createTask = TraceSession.Create(
                    sessionName, host, clientCertificate, providers, lifeTime, sessionLogger, _localizer);
                var checkedTask = createTask.ContinueWith(ct => {
                    if (!ct.IsCompletedSuccessfully) {
                        _ = this.TryRemove(sessionName, out var failedEntry);
                    }
                    return ct.Result;
                }, TaskScheduler.Default);
                return new TraceSessionEntry(checkedTask, _sessionIdleTime);
            });

            return entry.CreateTask;
        }

        public async Task<bool> CloseRemoteSession(string name) {
            if (this.TryRemove(name, out var entry)) {
                var session = await entry.CreateTask.ConfigureAwait(false);
                await session.CloseRemote().ConfigureAwait(false);
                return true;
            }
            return false;
        }
    }
}
