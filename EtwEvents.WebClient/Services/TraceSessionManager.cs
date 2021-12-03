using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using KdSoft.EtwEvents.WebClient.Models;
using KdSoft.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace KdSoft.EtwEvents.WebClient
{
    class TraceSessionManager: ConcurrentTimedLifeCycleManager<string, TraceSessionEntry>
    {
        readonly ILoggerFactory _loggerFactory;
        readonly IStringLocalizer<TraceSession> _localizer;
        readonly AggregatingNotifier<Models.TraceSessionStates> _changeNotifier;

        public TraceSessionManager(
            IConfiguration config,
            ILoggerFactory loggerFactory,
            IStringLocalizer<TraceSession> localizer
        ) : base(TimeSpan.TryParse(config?["TraceSession:ReapPeriod"], out var reapPeriod) ? reapPeriod : TimeSpan.FromMinutes(5), StringComparer.CurrentCultureIgnoreCase) {
            this._loggerFactory = loggerFactory;
            this._localizer = localizer;
            this._changeNotifier = new AggregatingNotifier<Models.TraceSessionStates>(GetSessionStates);
        }

        TraceSessionEntry CreateTraceSessionEntry(
            TraceSessionRequest request,
            X509Certificate2 clientCertificate,
            ILogger<TraceSession> sessionLogger
        ) {
            var createTask = new Lazy<Task<TraceSession>>(
                () => TraceSession.Create(request, clientCertificate, sessionLogger, _changeNotifier, _localizer)
            );
            return new TraceSessionEntry(createTask, request.LifeTime);
        }

        /// <summary>
        /// Opens a new trace session, or returns an already open one with the same name.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="host"></param>
        /// <param name="clientCertificate"></param>
        /// <param name="providers"></param>
        /// <param name="lifeTime"></param>
        /// <returns></returns>
        /// <remarks>When session was opened for first time, restartedProviders can be empty but not null.
        /// In other words, when restartedProviders is null, then the session was already open.</remarks>
        public async Task<Models.OpenSessionState> OpenSession(TraceSessionRequest request, X509Certificate2 clientCertificate) {
            var sessionLogger = _loggerFactory.CreateLogger<TraceSession>();
            var entry = this.GetOrAdd(request.Name, sessionName => CreateTraceSessionEntry(request, clientCertificate, sessionLogger));

            // we created the task using Lazy<T> so it is not already running when accessed for the first time,
            // avoiding a race condition where TraceSession.Create() might run multiple times with the same arguments;
            bool justStarted = !entry.SessionTask.IsValueCreated;
            TraceSession traceSession;
            try {
                // runs TraceSession.Create() when TraceSessionEntry instance is awaited for the first time
                traceSession = await entry.ConfigureAwait(false);
            }
            catch (Exception ex) {
                _ = TryRemove(request.Name, out var failedEntry);
                sessionLogger.LogError(ex, "Error in {method}", nameof(OpenSession));
                throw;
            }

            var openSessionState = traceSession.GetSessionStateSnapshot<Models.OpenSessionState>();

            if (justStarted) {
                openSessionState.RestartedProviders = traceSession.RestartedProviders;
                openSessionState.AlreadyOpen = false;
            }
            else {
                openSessionState.RestartedProviders = ImmutableList<string>.Empty;
                openSessionState.AlreadyOpen = true;
            }

            try {
                await PostSessionStateChange().ConfigureAwait(false);
            }
            catch (Exception ex) {
                sessionLogger.LogError(ex, "Error in {method}", nameof(PostSessionStateChange));
            }

            return openSessionState;
        }

        public async Task<bool> CloseRemoteSession(string name) {
            if (TryRemove(name, out var entry)) {
                var traceSession = await entry.ConfigureAwait(false);
                await traceSession.CloseRemote().ConfigureAwait(false);
                await PostSessionStateChange().ConfigureAwait(false);
                return true;
            }
            return false;
        }

        public async Task<Models.TraceSessionStates> GetSessionStates() {
            var sessionEntries = this.GetSnapshot();
            var sessionTasks = sessionEntries.Select(se => se.Value.SessionTask.Value);
            var sessions = await Task.WhenAll(sessionTasks).ConfigureAwait(false);
            var sessionStateTasks = sessions.Select(ses => ses.GetSessionState());

            var sessionStates = await Task.WhenAll(sessionStateTasks).ConfigureAwait(false);
            var ssb = ImmutableArray.CreateBuilder<TraceSessionState>();
            for (int indx = 0; indx < sessionStates.Length; indx++) {
                var ss = sessionStates[indx];
                if (ss != null)
                    ssb.Add(ss);
            }
            return new Models.TraceSessionStates { Sessions = ssb.ToImmutableArray() };
        }

        public IAsyncEnumerable<Models.TraceSessionStates> GetSessionStateChanges() {
            return _changeNotifier.GetNotifications();
        }

        public ValueTask PostSessionStateChange() {
            return _changeNotifier.PostNotification();
        }
    }
}
