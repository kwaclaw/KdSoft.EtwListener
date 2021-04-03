using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KdSoft.EtwEvents.Client.Shared;
using KdSoft.EtwEvents.Server;
using KdSoft.EtwLogging;
using LaunchDarkly.EventSource;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KdSoft.EtwEvents.PushAgent
{
    public class Worker: BackgroundService
    {
        readonly HostBuilderContext _context;
        readonly IOptions<ControlOptions> _controlOptions;
        readonly IOptions<EventQueueOptions> _eventQueueOptions;
        readonly IEventSinkFactory _sinkFactory;
        readonly ILoggerFactory _loggerFactory;
        readonly ILogger<Worker> _logger;
        readonly HttpClient _http;
        readonly HttpClientCertificateHandler _httpCertHandler;
        readonly JsonSerializerOptions _jsonOptions;

        EventSource? _eventSource;
        RealTimeTraceSession? _session;

        public Worker(
            HostBuilderContext context,
            IOptions<ControlOptions> controlOptions,
            IOptions<EventQueueOptions> eventQueueOptions,
            IEventSinkFactory sinkFactory,
            ILoggerFactory loggerFactory
        ) {
            this._context = context;
            this._controlOptions = controlOptions;
            this._eventQueueOptions = eventQueueOptions;
            this._sinkFactory = sinkFactory;
            this._loggerFactory = loggerFactory;
            this._logger = loggerFactory.CreateLogger<Worker>();

            _jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

            _httpCertHandler = new HttpClientCertificateHandler(controlOptions.Value.ClientCertificate);
            _http = new HttpClient(_httpCertHandler);
        }

        string EventSessionOptionsPath => Path.Combine(_context.HostingEnvironment.ContentRootPath, "eventSession.json");
        string EventSinkOptionsPath => Path.Combine(_context.HostingEnvironment.ContentRootPath, "eventSink.json");

        bool LoadSessionOptions(out EventSessionOptions options) {
            try {
                var sessionOptionsJson = File.ReadAllText(EventSessionOptionsPath);
                options = JsonSerializer.Deserialize<EventSessionOptions>(sessionOptionsJson, _jsonOptions) ?? new EventSessionOptions();
                return true;
            }
            catch (Exception ex) {
                options = new EventSessionOptions();
                _logger.LogError(ex, "Error loading event session options.");
                return false;
            }
        }

        bool SaveSessionOptions(EventSessionOptions options) {
            try {
                var json = JsonSerializer.Serialize(options, _jsonOptions);
                File.WriteAllText(EventSessionOptionsPath, json);
                return true;
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error saving event session options.");
                return false;
            }
        }

        bool LoadSinkOptions(out EventSinkOptions options) {
            try {
                var sinkOptionsJson = File.ReadAllText(EventSinkOptionsPath);
                options = JsonSerializer.Deserialize<EventSinkOptions>(sinkOptionsJson, _jsonOptions) ?? new EventSinkOptions();
                return true;
            }
            catch (Exception ex) {
                options = new EventSinkOptions();
                _logger.LogError(ex, "Error loading event sink options.");
                return false;
            }
        }

        bool SaveProviderSettings(IEnumerable<ProviderSetting> providers) {
            if (LoadSessionOptions(out var options)) {
                options.Providers = providers.Select(p => new ProviderOptions {
                    Name = p.Name, 
                    Level = (Microsoft.Diagnostics.Tracing.TraceEventLevel)p.Level,
                    MatchKeywords = p.MatchKeywords
                }).ToList();
                return SaveSessionOptions(options);
            }
            return false;
        }

        bool SaveFilterSettings(string csharpFilter) {
            if (LoadSessionOptions(out var options)) {
                options.Filter = csharpFilter;
                return SaveSessionOptions(options);
            }
            return false;
        }

        #region Control Channel

        async Task ProcessEvent(ControlEvent sse) {
            var opts = _controlOptions.Value;
            var ses = _session;
            if (ses == null)
                return;

            ImmutableArray<Diagnostic> diagnostics;
            BuildFilterResult filterResult;

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
                case "GetState":
                    await SendStateUpdate().ConfigureAwait(false);
                    break;
                case "UpdateProviders":
                    // WithDiscardUnknownFields does currently not work, so we should fix this at source
                    var providerSettingsList = ProviderSettingsList.Parser.WithDiscardUnknownFields(true).ParseJson(sse.Data);
                    var providerSettings = providerSettingsList.ProviderSettings;
                    if (providerSettings != null && ses != null) {
                        var providersToBeDisabled = ses.EnabledProviders.Select(ep => ep.Name).ToHashSet();
                        foreach (var setting in providerSettings) {
                            ses.EnableProvider(setting);
                            providersToBeDisabled.Remove(setting.Name);
                        }
                        foreach (var providerName in providersToBeDisabled) {
                            ses.DisableProvider(providerName);
                        }
                        SaveProviderSettings(providerSettings);
                    }
                    await SendStateUpdate().ConfigureAwait(false);
                    break;
                case "ApplyFilter":
                    var filterRequest = SetFilterRequest.Parser.ParseJson(sse.Data);
                    //var filterRequest = JsonSerializer.Deserialize<SetFilterRequest>(sse.Data, _jsonOptions);
                    if (filterRequest == null)
                        return;
                    diagnostics = ses.SetFilter(filterRequest.CsharpFilter);
                    filterResult = new BuildFilterResult().AddDiagnostics(diagnostics);
                    await PostMessage($"Agent/ApplyFilterResult?eventId={sse.Id}", filterResult).ConfigureAwait(false);
                    if (diagnostics.Length == 0) {
                        SaveFilterSettings(filterRequest.CsharpFilter);
                        await SendStateUpdate().ConfigureAwait(false);
                    }
                    break;
                case "TestFilter":
                    var testRequest = TestFilterRequest.Parser.ParseJson(sse.Data);
                    //var testRequest = JsonSerializer.Deserialize<TestFilterRequest>(sse.Data, _jsonOptions);
                    if (testRequest == null)
                        return;
                    diagnostics = RealTimeTraceSession.TestFilter(testRequest.CsharpFilter);
                    filterResult = new BuildFilterResult().AddDiagnostics(diagnostics);
                    await PostMessage($"Agent/TestFilterResult?eventId={sse.Id}", filterResult).ConfigureAwait(false);
                    break;
                case "UpdateEventSink":
                    //
                    break;
                default:
                    break;
            }
        }

        Task PostMessage<T>(string path, T content) {
            var opts = _controlOptions.Value;
            var postUri = new Uri(opts.Uri, path);
            var httpMsg = new HttpRequestMessage(HttpMethod.Post, postUri);

            var mediaType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);
            httpMsg.Content = JsonContent.Create<T>(content, mediaType, _jsonOptions);

            return _http.SendAsync(httpMsg);
        }

        Task SendStateUpdate() {
            //var agentName = _httpCertHandler.ClientCert.GetNameInfo(X509NameType.SimpleName, false);
            //var agentEmail = _httpCertHandler.ClientCert.GetNameInfo(X509NameType.EmailName, false);
            var state = new Models.AgentState {
                EnabledProviders = _session?.EnabledProviders.ToImmutableList() ?? ImmutableList<EtwLogging.ProviderSetting>.Empty,
                // Id = string.IsNullOrWhiteSpace(agentEmail) ? agentName : $"{agentName} ({agentEmail})",
                Id = string.Empty,  // will be filled in on server using the client certificate
                Host = Dns.GetHostName(),
                Site = _context.Configuration["Site"],
                FilterBody = _session?.GetCurrentFilterBody()
            };
            return PostMessage("Agent/UpdateState", state);
        }

        async void EventReceived(object? sender, MessageReceivedEventArgs e) {
            try {
                var lastEventIdStr = string.IsNullOrEmpty(e.Message.LastEventId) ? "" : $"-{e.Message.LastEventId}";
                var messageDataStr = string.IsNullOrEmpty(e.Message.Data) ? "" : $", {e.Message.Data}";
                _logger?.LogInformation($"{nameof(EventReceived)}: {e.EventName}{lastEventIdStr}{messageDataStr}");
                await ProcessEvent(new ControlEvent { Event = e.EventName, Id = e.Message.LastEventId ?? "", Data = e.Message.Data ?? "" }).ConfigureAwait(false);
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
            try {
                LoadSessionOptions(out var sessionOptions);
                LoadSinkOptions(out var sinkOptions);

                _eventSource = StartSSE();

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

                var diagnostics = session.SetFilter(sessionOptions.Filter);
                if (!diagnostics.IsEmpty) {
                    logger.LogError("Filter compilation failed.");
                }

                // enable the providers
                foreach (var provider in sessionOptions.Providers) {
                    var setting = new ProviderSetting();
                    setting.Name = provider.Name;
                    setting.Level = (TraceEventLevel)provider.Level;
                    setting.MatchKeywords = provider.MatchKeywords;
                    session.EnableProvider(setting);
                }

                var serOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

                var optsJson = JsonSerializer.Serialize(sinkOptions.Definition.Options, serOpts);
                var credsJson = JsonSerializer.Serialize(sinkOptions.Definition.Credentials, serOpts);

                await using (var sink = await _sinkFactory.Create(optsJson, credsJson).ConfigureAwait(false)) {
                    var processorLogger = _loggerFactory.CreateLogger<PersistentEventProcessor>();
                    using (var processor = new PersistentEventProcessor(sink, _eventQueueOptions, stoppingToken, processorLogger, sessionOptions.BatchSize)) {
                        var maxWriteDelay = TimeSpan.FromMilliseconds(sessionOptions.MaxWriteDelayMSecs);
                        await processor.Process(session, maxWriteDelay, stoppingToken).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Failure running service.");
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
