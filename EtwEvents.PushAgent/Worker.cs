using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KdSoft.EtwEvents.Client.Shared;
using KdSoft.EtwEvents.Server;
using LaunchDarkly.EventSource;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KdSoft.EtwEvents.PushAgent
{
    public class Worker: BackgroundService
    {
        const string ClientCertCN = "Elekta-SmartClinic-MQAddin";
        const string ClientCertHeader = "X-ARR-ClientCert";

        readonly IOptions<ControlOptions> _controlOptions;
        readonly IOptions<EventQueueOptions> _eventQueueOptions;
        readonly IOptions<EventSessionOptions> _sessionOptions;
        readonly IOptions<EventSinkOptions> _sinkOptions;
        readonly IEventSinkFactory _sinkFactory;
        readonly ILoggerFactory _loggerFactory;
        readonly ILogger<Worker> _logger;
        readonly HttpClient _http;
        readonly HttpClientHandler _httpCertHandler;

        EventSource? _eventSource;
        RealTimeTraceSession? _session;

        public Worker(
            IOptions<ControlOptions> controlOptions,
            IOptions<EventQueueOptions> eventQueueOptions,
            IOptions<EventSessionOptions> sessionOptions,
            IOptions<EventSinkOptions> sinkOptions,
            IEventSinkFactory sinkFactory,
            ILoggerFactory loggerFactory
        ) {
            this._controlOptions = controlOptions;
            this._eventQueueOptions = eventQueueOptions;
            this._sessionOptions = sessionOptions;
            this._sinkOptions = sinkOptions;
            this._sinkFactory = sinkFactory;
            this._loggerFactory = loggerFactory;
            this._logger = loggerFactory.CreateLogger<Worker>();
            _httpCertHandler = new HttpClientCertificateHandler(ClientCertCN, ClientCertHeader);
            _http = new HttpClient(_httpCertHandler);
        }

        #region Control Channel

        async Task ProcessEvent(ControlEvent sse) {
            switch (sse.Event) {
                case "ChangeLogLevel":
                    //
                    break;
                case "Start":
                    //
                    break;
                case "Stop":
                    //
                    break;
                default:
                    break;
            }
        }

        async void EventReceived(object? sender, MessageReceivedEventArgs e) {
            try {
                _logger?.LogInformation($"{nameof(EventReceived)}: {e.EventName}-{e.Message.LastEventId}, {e.Message.Data}");
                await ProcessEvent(new ControlEvent { Event = e.EventName, Id = e.Message.LastEventId, Data = e.Message.Data }).ConfigureAwait(false);
            }
            catch (Exception ex) {
                _logger?.LogAllErrors(ex, $"Error in {nameof(EventReceived)}:\n");
            }
        }

        void EventError(object? sender, ExceptionEventArgs e) {
            _logger?.LogAllErrors(e.Exception, $"Error in {nameof(EventError)}:\n");
        }

        void EventSourceStateChanged(object? sender, StateChangedEventArgs e) {
            _logger?.LogInformation($"{nameof(EventSourceStateChanged)}: {e.ReadyState}");
        }


        public EventSource StartSSE() {
            var opts = _controlOptions.Value;
            var evtUri = new Uri(opts.Uri, "Agent/GetEvents");
            var cfgBuilder = Configuration.Builder(evtUri).HttpClient(_http);
            if (opts.InitialRetryDelay != null)
                cfgBuilder.InitialRetryDelay(opts.InitialRetryDelay.Value);
            if (opts.MaxRetryDelay != null)
                cfgBuilder.MaxRetryDelay(opts.MaxRetryDelay.Value);
            if (opts.BackoffResetThreshold != null)
                cfgBuilder.BackoffResetThreshold(opts.BackoffResetThreshold.Value);
            var config = cfgBuilder.Build();

            var evt = new EventSource(config);
            evt.MessageReceived += EventReceived;
            evt.Error += EventError;
            evt.Opened += EventSourceStateChanged;
            evt.Closed += EventSourceStateChanged;

            var startTask = evt.StartAsync();
            return evt;
        }

        #endregion

        #region ETW Events

        #endregion

        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            _eventSource = StartSSE();

            var sopts = _sessionOptions.Value;

            var logger = _loggerFactory.CreateLogger<RealTimeTraceSession>();
            var session = new RealTimeTraceSession("default", TimeSpan.MaxValue, logger, false);
            this._session = session;

            stoppingToken.Register(() => {
                var ses = _session;
                if (ses != null) {
                    _session = null;
                    ses.Dispose();
                }
                var evt = _eventSource;
                if (evt != null) {
                    _eventSource = null;
                    evt.Close();
                }
            });


            session.GetLifeCycle().Used();

            // enable the providers
            foreach (var provider in _sessionOptions.Value.Providers) {
                var setting = new EtwLogging.ProviderSetting();
                setting.Name = provider.Name;
                setting.Level = (EtwLogging.TraceEventLevel)provider.Level;
                setting.MatchKeywords = provider.MatchKeyWords;
                session.EnableProvider(setting);
            }

            var sinkOpts = _sinkOptions.Value;
            var serOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

            var optsJson = JsonSerializer.Serialize(sinkOpts.Definition.Options, serOpts);
            var credsJson = JsonSerializer.Serialize(sinkOpts.Definition.Credentials, serOpts);

            await using (var sink = await _sinkFactory.Create(optsJson, credsJson).ConfigureAwait(false)) {
                var processorLogger = _loggerFactory.CreateLogger<PersistentEventProcessor>();
                using (var processor = new PersistentEventProcessor(sink, _eventQueueOptions, stoppingToken, processorLogger, _sessionOptions.Value.BatchSize)) {
                    var maxWriteDelay = TimeSpan.FromMilliseconds(_sessionOptions.Value.MaxWriteDelayMSecs);
                    await processor.Process(session, maxWriteDelay, stoppingToken).ConfigureAwait(false);
                }
            }
        }

        public override void Dispose() {
            base.Dispose();
            _http?.Dispose();
            _eventSource?.Dispose();
            _session?.Dispose();
        }
    }
}
