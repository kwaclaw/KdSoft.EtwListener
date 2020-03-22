using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using EtwEvents.WebClient.Models;
using Google.Protobuf.WellKnownTypes;
using Grpc.Net.Client;
using KdSoft.EtwLogging;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EtwEvents.WebClient
{
    sealed class TraceSession: IAsyncDisposable, IDisposable
    {
        readonly GrpcChannel _channel;
        readonly EtwListener.EtwListenerClient _etwClient;
        readonly ILogger<TraceSession> _logger;
        readonly IStringLocalizer<TraceSession> _;
        readonly EventSinkHolder _eventSinks;

        EventSession? _eventSession;
        CancellationTokenSource _eventCts;

        readonly object _syncObj = new object();

        public const int StopTimeoutMilliseconds = 3000;

        TraceSession(
            string name,
            ImmutableList<string> enabledProviders,
            GrpcChannel channel,
            EtwListener.EtwListenerClient etwClient,
            ILogger<TraceSession> logger,
            IStringLocalizer<TraceSession> localizer
        ) {
            this.Name = name;
            this.EnabledProviders = enabledProviders;
            _channel = channel;
            _etwClient = etwClient;
            _logger = logger;
            _ = localizer;
            _eventSinks = new EventSinkHolder();
            _eventCts = new CancellationTokenSource();
        }

        public string Name { get; }
        public ImmutableList<string> EnabledProviders { get; private set; }
        public EventSinkHolder EventSinks => this._eventSinks;
        public Task EventStream { get; private set; } = Task.CompletedTask;

        static GrpcChannel CreateChannel(string host, X509Certificate2 clientCertificate) {
            var handler = new HttpClientHandler();
            handler.ClientCertificates.Add(clientCertificate);
            var channel = GrpcChannel.ForAddress(host, new GrpcChannelOptions {
                HttpClient = new HttpClient(handler, true),
                DisposeHttpClient = true
            });
            return channel;
        }

        public static async Task<(TraceSession traceSession, IImmutableList<string> restartedProviders)> Create(
            string name,
            string host,
            X509Certificate2 clientCertificate,
            IReadOnlyList<ProviderSetting> providers,
            Duration lifeTime,
            ILogger<TraceSession> logger,
            IStringLocalizer<TraceSession> localizer
        ) {
            var channel = CreateChannel(host, clientCertificate);

            try {
                var client = new EtwListener.EtwListenerClient(channel);

                var openEtwSession = new OpenEtwSession {
                    Name = name,
                    LifeTime = lifeTime,
                    TryAttach = false,
                };
                openEtwSession.ProviderSettings.AddRange(providers);

                var reply = await client.OpenSessionAsync(openEtwSession);
                var enabledProviders = reply.Results.Select(r => r.Name).ToImmutableList();
                var restartedProviders = reply.Results.Where(r => r.Restarted).Select(r => r.Name).ToImmutableList();

                var traceSession =  new TraceSession(name, enabledProviders, channel, client, logger, localizer);
                return (traceSession, restartedProviders);
            }
            catch {
                channel.Dispose();
                throw;
            }
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
                await DisposeAsync().ConfigureAwait(false);
            }
        }

        public async Task UpdateSessionState() {
            var etwSession = await _etwClient.GetSessionAsync(new StringValue { Value = Name });
            this.EnabledProviders = etwSession.EnabledProviders.Select(r => r.Name).ToImmutableList();
        }

        async Task<EventSession> StartEventsInternal(IOptionsMonitor<EventSessionOptions> optionsMonitor) {
            if (optionsMonitor == null)
                throw new ArgumentNullException(nameof(optionsMonitor));

            var etwRequest = new EtwEventRequest {
                SessionName = this.Name
            };

            EventSession eventSession;
            Task<bool>? stopTask = null;
            CancellationTokenSource? stopEventCts = null;

            lock (_syncObj) {
                if (_eventSession != null) {
                    stopEventCts = _eventCts;
                    stopEventCts.Cancel();
                    stopTask = _eventSession.Stop();
                }
                eventSession = new EventSession(_etwClient, etwRequest, optionsMonitor, _eventSinks);
                _eventSession = eventSession;
                _eventCts = new CancellationTokenSource();
            }

            if (stopTask != null) {
                try {
                    await stopTask.ConfigureAwait(false);
                }
                catch {
                    // ignore errors and continue
                }
                finally {
                    if (stopEventCts != null)
                        stopEventCts.Dispose();
                }
            }

            try {
                bool newlyStarted = await eventSession.Run(_eventCts.Token).ConfigureAwait(false);
                if (!newlyStarted)
                    throw new InvalidOperationException(_.GetString("Event session already started."));
                //TODO how should we handle eventSession.FailedEventSinks?
            }
            catch (OperationCanceledException) {
                // typically ignored in this scenario
            }

            return eventSession;
        }

        public Task<EventSession> StartEvents(IOptionsMonitor<EventSessionOptions> optionsMonitor) {
            var eventsTask = StartEventsInternal(optionsMonitor);
            this.EventStream = eventsTask;
            return eventsTask;
        }

        public async Task<bool> StopEvents() {
            Task<bool>? stopTask = null;
            CancellationTokenSource? stopEventCts = null;

            lock (_syncObj) {
                if (_eventSession != null) {
                    stopEventCts = _eventCts;
                    stopEventCts.CancelAfter(StopTimeoutMilliseconds);
                    stopTask = _eventSession.Stop();
                    _eventSession = null;
                }
            }

            if (stopTask != null) {
                try {
                    return await stopTask.ConfigureAwait(false);
                }
                catch {
                    // ignore errors and continue
                }
                finally {
                    if (stopEventCts != null)
                        stopEventCts.Dispose();
                }
            }

            return false;
        }

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

        public async ValueTask DisposeAsync() {
            try {
                await StopEvents().ConfigureAwait(false);
            }
            finally {
                _channel.Dispose();
            }
        }

        public void Dispose() {
            _eventSession?.Dispose();
        }
    }
}
