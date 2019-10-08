using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using KdSoft.EtwLogging;
using KdSoft.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EtwEvents.WebClient
{
    public class TraceSessionManager: ConcurrentTimedLifeCycleManager<string, TraceSessionEntry>
    {
        readonly IServiceProvider _services;
        readonly ILoggerFactory _loggerFactory;
        readonly TimeSpan _sessionIdleTime;

        public TraceSessionManager(IConfiguration config, IServiceProvider services, ILoggerFactory loggerFactory): 
            base(TimeSpan.TryParse(config?["ReapPeriod"], out var reapPeriod) ? reapPeriod : TimeSpan.FromMinutes(5))
        {
            this._services = services;
            this._loggerFactory = loggerFactory;
            if (!TimeSpan.TryParse(config?["SessionIdleTime"], out this._sessionIdleTime))
                this._sessionIdleTime = TimeSpan.FromMinutes(5);
        }


        public Task<TraceSession> OpenSession(
            string name,
            string host,
            ChannelCredentials credentials,
            IReadOnlyList<ProviderSetting> providers,
            Duration lifeTime
        ) {
            var entry = this.GetOrAdd(name, sessionName => {
                var sessionLogger = _loggerFactory.CreateLogger<TraceSession>();
                var createTask = TraceSession.Create(sessionName, host, credentials, providers, lifeTime, sessionLogger);
                return new TraceSessionEntry(createTask, _sessionIdleTime);
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
