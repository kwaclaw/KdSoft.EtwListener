using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using EtwEvents.WebClient.Models;
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
        public Task<OpenSessionState> OpenSession(
            string name,
            string host,
            X509Certificate2 clientCertificate,
            IReadOnlyList<ProviderSetting> providers,
            Duration lifeTime
        ) {
            Task<OpenSessionState>? result = null;

            // NOTE: the value factory might be executed multiple times with this overload of GetOrAdd,
            //       if this is a problem then we need to use Lazy<T> instead.
            var entry = this.GetOrAdd(name, sessionName => {
                var sessionLogger = _loggerFactory.CreateLogger<TraceSession>();
                var createTask = TraceSession.Create(
                    sessionName, host, clientCertificate, providers, lifeTime, sessionLogger, _localizer);

                var checkedTask = createTask.ContinueWith<TraceSession>(ct => {
                    if (!ct.IsCompletedSuccessfully) {
                        _ = this.TryRemove(sessionName, out var failedEntry);
                    }
                    PostSessionStateChange().AsTask().ContinueWith(pst => {
                        var ex = pst.Exception;
                        sessionLogger.LogError(ex, "Error in PostSessionStateChange");
                    }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Current);  //TODO deadlock here
                    return ct.Result.traceSession;
                }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Current);

                result = createTask.ContinueWith<OpenSessionState>(ct => {
                    var session = ct.Result.traceSession;
                    var openSessionState = session.GetSessionStateSnapshot<OpenSessionState>();
                    openSessionState.RestartedProviders = ct.Result.restartedProviders;
                    openSessionState.AlreadyOpen = false;
                    return openSessionState;
                }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Current);

                return new TraceSessionEntry(checkedTask, lifeTime.ToTimeSpan());
            });

            if (result != null)
                return result;

            return entry.SessionTask.ContinueWith<OpenSessionState>(st => {
                var session = st.Result;
                var openSessionState = session.GetSessionStateSnapshot<OpenSessionState>();
                openSessionState.RestartedProviders = ImmutableList<string>.Empty;
                openSessionState.AlreadyOpen = true;
                return openSessionState;
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Current);
        }

        public async Task<bool> CloseRemoteSession(string name) {
            if (this.TryRemove(name, out var entry)) {
                var session = await entry.SessionTask.ConfigureAwait(false);
                await session.CloseRemote().ConfigureAwait(false);
                await PostSessionStateChange().ConfigureAwait(false);
                return true;
            }
            return false;
        }

        public async Task<TraceSessionStates> GetSessionStates() {
            var sessionEntries = this.GetSnapshot();
            var sessionTasks = sessionEntries.Select(se => se.Value.SessionTask);
            var sessions = await Task.WhenAll(sessionTasks).ConfigureAwait(false);
            var sessionStateTasks = sessions.Select(s => s.GetSessionState());
            var sessionStates = await Task.WhenAll(sessionStateTasks).ConfigureAwait(false);
            return new TraceSessionStates { Sessions = sessionStates.ToImmutableArray() };
        }

        public async ValueTask PostSessionStateChange() {
            var changeEnumerators = _changeEnumerators;
            foreach (var enumerator in changeEnumerators) {
                try {
                    await enumerator.Advance().ConfigureAwait(false);
                }
                catch { }
            }
        }

        readonly object _enumeratorSync = new object();
        ImmutableList<ChangeEnumerator> _changeEnumerators = ImmutableList<ChangeEnumerator>.Empty;

        void AddEnumerator(ChangeEnumerator enumerator) {
            lock (_enumeratorSync) {
                _changeEnumerators = _changeEnumerators.Add(enumerator);
            }
        }

        void RemoveEnumerator(ChangeEnumerator enumerator) {
            lock (_enumeratorSync) {
                _changeEnumerators = _changeEnumerators.Remove(enumerator);
            }
        }

        public IAsyncEnumerable<TraceSessionStates> GetSessionStateChanges() {
            return new ChangeListener(this);
        }

        // https://anthonychu.ca/post/async-streams-dotnet-core-3-iasyncenumerable/
        class ChangeListener: IAsyncEnumerable<TraceSessionStates>
        {
            readonly TraceSessionManager _sessionManager;

            public ChangeListener(TraceSessionManager sessionManager) {
                this._sessionManager = sessionManager;
            }

            public IAsyncEnumerator<TraceSessionStates> GetAsyncEnumerator(CancellationToken cancellationToken = default) {
                return new ChangeEnumerator(_sessionManager, cancellationToken);
            }
        }

        // https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-8.0/async-streams
        class ChangeEnumerator: PendingAsyncEnumerator<TraceSessionStates>
        {
            readonly TraceSessionManager _sessionManager;

            public ChangeEnumerator(TraceSessionManager sessionManager, CancellationToken cancelToken) : base(cancelToken) {
                this._sessionManager = sessionManager;
                sessionManager.AddEnumerator(this);
            }

            public override ValueTask DisposeAsync() {
                _sessionManager.RemoveEnumerator(this);
                return default;
            }

            protected override Task<TraceSessionStates> GetNext() {
                return _sessionManager.GetSessionStates();
            }
        }
    }
}
