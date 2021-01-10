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
        ) : base(TimeSpan.TryParse(config?["ReapPeriod"], out var reapPeriod) ? reapPeriod : TimeSpan.FromMinutes(5), StringComparer.CurrentCultureIgnoreCase)
        {
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
        public async Task<Models.OpenSessionState> OpenSession(TraceSessionRequest request, X509Certificate2 clientCertificate ) {
            var sessionLogger = _loggerFactory.CreateLogger<TraceSession>();
            var entry = this.GetOrAdd(request.Name, sessionName => CreateTraceSessionEntry(request, clientCertificate, sessionLogger));

            // we created the task as not running/started, so we avoid a race condition where the same code
            // with the same arguments might run multiple times;
            // instead, we start the task now, handling the exception that indicates it was already started
            bool justStarted = !entry.SessionTask.IsValueCreated;

            TraceSession traceSession;
            try {
                traceSession = await entry.SessionTask.Value.ConfigureAwait(false);
            }
            catch (Exception ex) {
                _ = this.TryRemove(request.Name, out var failedEntry);
                sessionLogger.LogError(ex, $"Error in {nameof(OpenSession)}");
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
                await PostSessionStateChange();
            }
            catch (Exception ex) {
                sessionLogger.LogError(ex, $"Error in {nameof(PostSessionStateChange)}");
            }

            return openSessionState;
        }

        //public Task<Models.OpenSessionState> OpenSession(TraceSessionRequest request, X509Certificate2 clientCertificate ) {
        //    Task<(TraceSession traceSession, IImmutableList<string> restartedProviders)>? createTask = null;

        //    // NOTE: the value factory might be executed multiple times with this overload of GetOrAdd,
        //    //       if this is a problem then we need to use Lazy<T> instead.
        //    var entry = this.GetOrAdd(request.Name, sessionName => {
        //        var sessionLogger = _loggerFactory.CreateLogger<TraceSession>();
        //        createTask = TraceSession.Create(request, clientCertificate, sessionLogger, _changeNotifier, _localizer);

        //        var checkedTask = createTask.ContinueWith<TraceSession>(ct => {
        //            if (!ct.IsCompletedSuccessfully) {
        //                _ = this.TryRemove(sessionName, out var failedEntry);
        //            }
        //            PostSessionStateChange().AsTask().ContinueWith(pst => {
        //                var ex = pst.Exception;
        //                sessionLogger.LogError(ex, "Error in PostSessionStateChange");
        //            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Current);
        //            return ct.Result.traceSession;
        //        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Current);

        //        return new TraceSessionEntry(checkedTask, request.LifeTime);
        //    });

        //    // return OpenSessionState if we just created a new Session
        //    if (createTask != null && createTask.IsCompletedSuccessfully) {
        //        return createTask.ContinueWith<Models.OpenSessionState>(ct => {
        //            var session = ct.Result.traceSession;
        //            var openSessionState = session.GetSessionStateSnapshot<Models.OpenSessionState>();
        //            openSessionState.RestartedProviders = ct.Result.restartedProviders;
        //            openSessionState.AlreadyOpen = false;
        //            return openSessionState;
        //        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Current);
        //    }

        //    return entry.SessionTask.ContinueWith<Models.OpenSessionState>(st => {
        //        var session = st.Result;
        //        var openSessionState = session.GetSessionStateSnapshot<Models.OpenSessionState>();
        //        openSessionState.RestartedProviders = ImmutableList<string>.Empty;
        //        openSessionState.AlreadyOpen = true;
        //        return openSessionState;
        //    }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Current);
        //}

        public async Task<bool> CloseRemoteSession(string name) {
            if (this.TryRemove(name, out var entry)) {
                var session = await entry.SessionTask.Value.ConfigureAwait(false);
                await session.CloseRemote().ConfigureAwait(false);
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
