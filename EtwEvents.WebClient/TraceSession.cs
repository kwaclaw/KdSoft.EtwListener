using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using KdSoft.EtwLogging;
using Microsoft.Extensions.Logging;

namespace EtwEvents.WebClient
{
    public class SessionResult
    {
        public SessionResult(List<string> enabled, List<string> failed) {
            EnabledProviders = enabled;
            FailedProviders = failed;
        }

        public List<string> EnabledProviders { get; }
        public List<string> FailedProviders { get; }
    }

    public sealed class TraceSession: IAsyncDisposable, IDisposable
    {
        readonly Channel _channel;
        readonly EtwListener.EtwListenerClient _etwClient;
        readonly ILogger<TraceSession> _logger;
        EventSession? _eventSession;

        readonly object _syncObj = new object();

        TraceSession(
            string name,
            List<string> enabledProviders,
            List<string> failedProviders,
            Channel channel,
            EtwListener.EtwListenerClient etwClient,
            ILogger<TraceSession> logger
        ) {
            this.Name = name;
            this.EnabledProviders = enabledProviders;
            this.FailedProviders = failedProviders;
            _channel = channel;
            _etwClient = etwClient;
            _logger = logger;
        }

        public string Name { get; }
        public List<string> EnabledProviders { get; }
        public List<string> FailedProviders { get; }

        public static async Task<TraceSession> Create(
            string name,
            string host,
            ChannelCredentials credentials,
            IReadOnlyList<ProviderSetting> providers,
            Duration lifeTime,
            ILogger<TraceSession> logger
        ) {
            var channel = new Channel(host, credentials);
            try {
                var client = new EtwListener.EtwListenerClient(channel);

                var openEtwSession = new OpenEtwSession {
                    Name = name,
                    LifeTime = lifeTime,
                    TryAttach = false,
                };
                openEtwSession.ProviderSettings.AddRange(providers);

                var reply = await client.OpenSessionAsync(openEtwSession);
                var enabledProviders = reply.Results.Where(r => r.Success).Select(r => r.Name).ToList();
                var failedProviders = reply.Results.Where(r => !r.Success).Select(r => r.Name).ToList();

                return new TraceSession(name, enabledProviders, failedProviders, channel, client, logger);
            }
            catch {
                await channel.ShutdownAsync().ConfigureAwait(false);
                throw;
            }
        }

        internal async Task CloseRemote() {
            try {
                var closeEtwSession = new CloseEtwSession { Name = this.Name };
                var reply = await _etwClient.CloseSessionAsync(closeEtwSession);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Close Session error");
            }
            finally {
                await DisposeAsync().ConfigureAwait(false);
            }
        }

        public Task StartEvents(WebSocket webSocket) {
            if (webSocket == null)
                throw new ArgumentNullException(nameof(webSocket));

            var request = new EtwEventRequest {
                SessionName = this.Name
            };

            EventSession eventSession;
            lock (_syncObj) {
                if (_eventSession != null) {
                    _eventSession.Stop();
                }
                eventSession = new EventSession(_etwClient, webSocket, request);
                _eventSession = eventSession;
            }

            if (eventSession.Start())
                return Task.WhenAll(eventSession.ReceiveTask, eventSession.RunTask!);
            else
                return eventSession.ReceiveTask;
        }

        public Task StopEvents() {
            Task? runTask = null;
            Task? receiveTask = null;
            lock (_syncObj) {
                if (_eventSession != null) {
                    _eventSession.Stop();
                    runTask = _eventSession.RunTask;
                    receiveTask = _eventSession.ReceiveTask;
                    _eventSession = null;
                }
            }
            if (runTask == null)
                return receiveTask ?? Task.CompletedTask;
            else
                return Task.WhenAll(receiveTask!, runTask);
        }

        public async Task<SetFilterResult> SetCSharpFilter(string csharpFilter) {
            var setFilterRequest = new SetFilterRequest { SessionName = this.Name, CsharpFilter = csharpFilter };
            var result = await _etwClient.SetCSharpFilterAsync(setFilterRequest);
            return result;
        }

        public async ValueTask DisposeAsync() {
            try {
                await StopEvents().ConfigureAwait(false);
            }
            finally {
                await _channel.ShutdownAsync().ConfigureAwait(false);
            }
        }

        public void Dispose() {
            _eventSession?.Dispose();
        }
    }
}
