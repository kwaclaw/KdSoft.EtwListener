using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using EtwEvents.WebClient.Models;
using Google.Protobuf.WellKnownTypes;
using Grpc.Net.Client;
using KdSoft.EtwLogging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
        readonly GrpcChannel _channel;
        readonly EtwListener.EtwListenerClient _etwClient;
        readonly ILogger<TraceSession> _logger;
        EventSession? _eventSession;

        readonly object _syncObj = new object();

        public const int StopTimeoutMilliseconds = 3000;

        TraceSession(
            string name,
            List<string> enabledProviders,
            List<string> restartedProviders,
            GrpcChannel channel,
            EtwListener.EtwListenerClient etwClient,
            ILogger<TraceSession> logger
        ) {
            this.Name = name;
            this.EnabledProviders = enabledProviders;
            this.RestartedProviders = restartedProviders;
            _channel = channel;
            _etwClient = etwClient;
            _logger = logger;
        }

        public string Name { get; }
        public List<string> EnabledProviders { get; }
        public List<string> RestartedProviders { get; }

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

        public static async Task<TraceSession> Create(
            string name,
            string host,
            X509Certificate2 clientCertificate,
            IReadOnlyList<ProviderSetting> providers,
            Duration lifeTime,
            ILogger<TraceSession> logger
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
                var enabledProviders = reply.Results.Select(r => r.Name).ToList();
                var restartedProviders = reply.Results.Where(r => r.Restarted).Select(r => r.Name).ToList();

                return new TraceSession(name, enabledProviders, restartedProviders, channel, client, logger);
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
                _logger.LogError(ex, "Close Session error");
            }
            finally {
                await DisposeAsync().ConfigureAwait(false);
            }
        }

        public async Task StartEvents(WebSocket webSocket, IOptionsMonitor<EventSessionOptions> optionsMonitor) {
            if (webSocket == null)
                throw new ArgumentNullException(nameof(webSocket));
            if (optionsMonitor == null)
                throw new ArgumentNullException(nameof(optionsMonitor));

            var request = new EtwEventRequest {
                SessionName = this.Name
            };

            EventSession eventSession;
            Task? stopTask = null;
            lock (_syncObj) {
                if (_eventSession != null) {
                    stopTask = _eventSession.Stop(true, 0);
                }
                eventSession = new EventSession(_etwClient, webSocket, request, optionsMonitor, StopTimeoutMilliseconds);
                _eventSession = eventSession;
            }

            if (stopTask != null) {
                try {
                    await stopTask.ConfigureAwait(false);
                }
                catch {
                    // ignore errors and continue
                }
            }

            try {
                if (eventSession.Start())
                    await Task.WhenAll(eventSession.ReceiveTask, eventSession.RunTask!).ConfigureAwait(false);
                else
                    await eventSession.ReceiveTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                // typically ignored in this scenario
            }
        }

        public Task<bool> StopEvents() {
            lock (_syncObj) {
                if (_eventSession != null) {
                    var result = _eventSession.Stop(true, StopTimeoutMilliseconds);
                    _eventSession = null;
                    return result;
                }
            }
            return Task.FromResult(false);
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
