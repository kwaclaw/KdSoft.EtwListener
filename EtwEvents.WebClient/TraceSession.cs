using System;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Net.Client;
using KdSoft.EtwLogging;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KdSoft.EtwEvents.WebClient
{
    sealed class TraceSession: IAsyncDisposable, IDisposable
    {
        static ProviderSetting _nullProvider = new ProviderSetting();

        readonly int _batchSize;
        readonly Duration _maxWriteDelay;
        readonly GrpcChannel _channel;
        readonly EtwListener.EtwListenerClient _etwClient;
        readonly ILogger<TraceSession> _logger;
        readonly AggregatingNotifier<Models.TraceSessionStates> _changeNotifier;
        readonly IStringLocalizer<TraceSession> _;
        readonly object _syncObj = new object();

        EventSession? _eventSession;
        CancellationTokenSource? _eventCts;
        EtwSession _etwSession;
        Task _eventsTask = Task.CompletedTask;

        public const int StopTimeoutMilliseconds = 3000;
        public string Name { get; }
        public string Host => $"https://{_channel.Target}";
        public EventSinkHolder EventSinks { get; }
        public Task EventsTask => _eventsTask;

        #region Construction

        TraceSession(
            string name,
            ImmutableList<ProviderSetting> enabledProviders,
            int batchSize,
            Duration maxWriteDelay,
            GrpcChannel channel,
            EtwListener.EtwListenerClient etwClient,
            ILogger<TraceSession> logger,
            AggregatingNotifier<Models.TraceSessionStates> changeNotifier,
            IStringLocalizer<TraceSession> localizer
        ) {
            this.Name = name;
            this._batchSize = batchSize;
            this._maxWriteDelay = maxWriteDelay;
            this._etwSession = new EtwSession();
            this._etwSession.EnabledProviders.AddRange(enabledProviders);
            _channel = channel;
            _etwClient = etwClient;
            _logger = logger;
            _changeNotifier = changeNotifier;
            _ = localizer;
            EventSinks = new EventSinkHolder();
        }

        static GrpcChannel CreateChannel(string host, X509Certificate2 clientCertificate) {
#pragma warning disable CA2000 // Dispose objects before losing scope
            var handler = new HttpClientHandler();
#pragma warning restore CA2000 // Dispose objects before losing scope
            handler.ClientCertificates.Add(clientCertificate);
            var channel = GrpcChannel.ForAddress(host, new GrpcChannelOptions {
                HttpClient = new HttpClient(handler, true),
                DisposeHttpClient = true
            });
            return channel;
        }

        public static async Task<(TraceSession traceSession, IImmutableList<string> restartedProviders)> Create(
            Models.TraceSessionRequest request,
            X509Certificate2 clientCertificate,
            ILogger<TraceSession> logger,
            AggregatingNotifier<Models.TraceSessionStates> changeNotifier,
            IStringLocalizer<TraceSession> localizer
        ) {
            var channel = CreateChannel(request.Host, clientCertificate);

            try {
                var client = new EtwListener.EtwListenerClient(channel);

                var openEtwSession = new OpenEtwSession {
                    Name = request.Name,
                    LifeTime = request.LifeTime.ToDuration(),
                    TryAttach = false,
                };
                openEtwSession.ProviderSettings.AddRange(request.Providers);

                var reply = await client.OpenSessionAsync(openEtwSession);

                var matchingProviders = reply.Results.Select(r =>
                    request.Providers.FirstOrDefault(s => string.Equals(s.Name, r.Name, StringComparison.CurrentCultureIgnoreCase))
                    ??
                    _nullProvider
                );
                var enabledProviders = matchingProviders.Where(p => !object.ReferenceEquals(p, _nullProvider)).ToImmutableList();

                var restartedProviders = reply.Results.Where(r => r.Restarted).Select(r => r.Name).ToImmutableList();

                var traceSession = new TraceSession(
                    request.Name,
                    enabledProviders,
                    request.BatchSize,
                    new Duration { Nanos = request.MaxWriteDelayMS * 1000000 },
                    channel,
                    client,
                    logger,
                    changeNotifier,
                    localizer
                );
                return (traceSession, restartedProviders);
            }
            catch {
                channel.Dispose();
                throw;
            }
        }

        public async ValueTask DisposeAsync() {
            try {
                await StopEvents().ConfigureAwait(false);
            }
            finally {
                Dispose();
            }
        }

        public void Dispose() {
            _channel.Dispose();
            lock (_syncObj) {
                _eventSession?.Dispose();
                if (_eventCts != null) {
                    _eventCts.Dispose();
                    _eventCts = null;
                }
            }
        }

        #endregion

        #region TraceSessionState

        public async Task UpdateSessionState() {
            var etwSession = await _etwClient.GetSessionAsync(new StringValue { Value = Name });
            Interlocked.MemoryBarrier();
            _etwSession = etwSession;
            Interlocked.MemoryBarrier();
        }

        public T GetSessionStateSnapshot<T>() where T : Models.TraceSessionState, new() {
            Interlocked.MemoryBarrier();
            var etwSession = _etwSession;
            Interlocked.MemoryBarrier();

            var isCompleted = _eventsTask.IsCompleted;
            var result = new T {
                Name = Name ?? string.Empty,
                Host = Host,
                IsRunning = !isCompleted && etwSession.IsStarted && !etwSession.IsStopped,
                IsStopped = (isCompleted && etwSession.IsStarted) || etwSession.IsStopped,
                EnabledProviders = etwSession.EnabledProviders.ToImmutableList()
            };

            var activeEventSinks = EventSinks.ActiveEventSinks.Select(
                aes => new Models.EventSinkState { SinkType = aes.Value.GetType().Name, Name = aes.Key }
            ).ToImmutableArray();
            var eventSinks = activeEventSinks.AddRange(EventSinks.FailedEventSinks.Select(
                fes => new Models.EventSinkState {
                    SinkType = fes.Value.sink.GetType().Name,
                    Name = fes.Key,
                    Error = fes.Value.error?.Message ?? _.GetString("Write Failure"),
                }
            ));
            result.EventSinks = eventSinks;
            return result;
        }

        public async Task<Models.TraceSessionState?> GetSessionState() {
            try {
                await UpdateSessionState().ConfigureAwait(false);
                return GetSessionStateSnapshot<Models.TraceSessionState>();
            }
            catch (Exception ex) {
                _logger.LogError(ex, _.GetString("GetSessionState error."));
                return null;
            }
        }

        public ValueTask PostSessionStateChange() {
            return _changeNotifier.PostNotification();
        }

        #endregion

        #region Start/Stop Events

        string? StartEventsInternal(IOptionsMonitor<Models.EventSessionOptions> optionsMonitor, out Task eventsTask) {
            if (optionsMonitor == null)
                throw new ArgumentNullException(nameof(optionsMonitor));

            eventsTask = Task.CompletedTask;

            var etwRequest = new EtwEventRequest {
                SessionName = this.Name,
                BatchSize = this._batchSize,
                MaxWriteDelay = this._maxWriteDelay
            };

            EventSession eventSession;
            CancellationTokenSource? eventCts = null;

            lock (_syncObj) {
                if (_eventSession == null) {
                    eventSession = new EventSession(_etwClient, etwRequest, optionsMonitor, EventSinks, _changeNotifier);
                    _eventSession = eventSession;
                    _eventCts = eventCts = new CancellationTokenSource();
                }
                else {
                    eventSession = _eventSession;
                    eventCts = _eventCts;
                }
            }

            if (eventCts == null)
                return _.GetString("Event session cannot be restarted.");

            bool newlyStarted = eventSession.Run(eventCts.Token, out eventsTask);
            if (!newlyStarted)
                return _.GetString("Event session already started.");

            //TODO how should we handle eventSession.FailedEventSinks?

            return null;
        }

        public string? StartEvents(IOptionsMonitor<Models.EventSessionOptions> optionsMonitor) {
            var result = StartEventsInternal(optionsMonitor, out var eventsTask);
            this._eventsTask = eventsTask;
            eventsTask.ContinueWith(t => {
                if (t.IsFaulted)
                    _logger.LogError(t.Exception, $"Error in {nameof(eventsTask)}");
                PostSessionStateChange();
            });
            return result;
        }

        /// <summary>
        /// Stops events from being delivered.
        /// Since we cannot restart events in a real time session, this API is of limited usefulness.
        /// We can achieve the same result by simply calling CloseRemote().
        /// </summary>
        public async Task<bool> StopEvents() {
            Task<bool>? stopTask = null;
            CancellationTokenSource? stopEventCts = null;

            lock (_syncObj) {
                if (_eventSession != null) {
                    stopEventCts = _eventCts;
                    if (stopEventCts != null) {
                        _eventCts = null;
                        stopEventCts.CancelAfter(StopTimeoutMilliseconds);
                        stopTask = _eventSession.Stop();
                    }
                }
            }

            if (stopEventCts != null && stopTask != null) {
                try {
                    return await stopTask.ConfigureAwait(false);
                }
                catch {
                    // ignore errors and continue
                }
                finally {
                    stopEventCts.Dispose();
                }
            }

            return false;
        }

        internal async Task CloseRemote() {
            try {
                var closeEtwSession = new CloseEtwSession { Name = this.Name };
                var reply = await _etwClient.CloseSessionAsync(closeEtwSession);
            }
            catch (Exception ex) {
                _logger.LogError(ex, _.GetString("Close Session error"));
            }
            finally {
                // DisposeAsync() would call StopEvents after the remote session is closed!
                Dispose();
            }
        }

        #endregion

        #region CSharp Filter

        public async Task<BuildFilterResult> SetCSharpFilter(string csharpFilter) {
            var setFilterRequest = new KdSoft.EtwLogging.SetFilterRequest { SessionName = this.Name, CsharpFilter = csharpFilter };
            var result = await _etwClient.SetCSharpFilterAsync(setFilterRequest);
            return result;
        }

        public static async Task<BuildFilterResult> TestCSharpFilter(string host, X509Certificate2 clientCertificate, string csharpFilter) {
            var channel = CreateChannel(host, clientCertificate);
            try {
                var client = new EtwListener.EtwListenerClient(channel);
                var testFilterRequest = new KdSoft.EtwLogging.TestFilterRequest { CsharpFilter = csharpFilter };
                var result = await client.TestCSharpFilterAsync(testFilterRequest);
                return result;
            }
            finally {
                channel.Dispose();
            }
        }

        #endregion
    }
}
