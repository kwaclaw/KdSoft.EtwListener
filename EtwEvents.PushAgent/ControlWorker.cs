using System;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KdSoft.EtwEvents.Client;
using KdSoft.EtwLogging;
using LaunchDarkly.EventSource;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KdSoft.EtwEvents.PushAgent
{
    class ControlWorker: BackgroundService
    {
        readonly HostBuilderContext _context;
        readonly IServiceProvider _services;
        readonly HttpClient _http;
        readonly IOptions<ControlOptions> _controlOptions;
        readonly SessionConfig _sessionConfig;
        readonly ILogger<ControlWorker> _logger;
        readonly JsonSerializerOptions _jsonOptions;

        EventSource? _eventSource;
        IServiceScope? _workerScope;

        SessionWorker? _sessionWorker;  // only valid when _sessionWorkerAvailable != 0
        int _sessionWorkerAvailable = 0;
        SessionWorker? SessionWorker => _sessionWorkerAvailable == 0 ? null : _sessionWorker!;

        public ControlWorker(
            HostBuilderContext context,
            IServiceProvider services,
            HttpClient http,
            IOptions<ControlOptions> controlOptions,
            SessionConfig sessionConfig,
            ILogger<ControlWorker> logger
        ) {
            this._context = context;
            this._services = services;
            this._http = http;
            this._controlOptions = controlOptions;
            this._sessionConfig = sessionConfig;
            this._logger = logger;

            _jsonOptions = new JsonSerializerOptions {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                AllowTrailingCommas = true,
                WriteIndented = true
            };
        }

        #region Control Channel

        async Task ProcessEvent(ControlEvent sse) {
            switch (sse.Event) {
                case "Start":
                    if (_sessionWorkerAvailable != 0) {
                        _logger?.LogInformation("Session already starting.");
                        return;
                    }
                    var started = await StartWorker(default).ConfigureAwait(false);
                    if (started) {
                        await SendStateUpdate().ConfigureAwait(false);
                    }
                    return;
                case "Stop":
                    if (_sessionWorkerAvailable == 0) {
                        _logger?.LogInformation("Session already stopping.");
                        return;
                    }
                    break;
                case "GetState":
                    await SendStateUpdate().ConfigureAwait(false);
                    return;
                default:
                    break;
            }

            // need different logic depending on whether a session is active or not
            SessionWorker? worker = _sessionWorkerAvailable == 0 ? null : SessionWorker;

            FilterModel? filterModel;
            BuildFilterResult filterResult;

            switch (sse.Event) {
                case "ChangeLogLevel":
                    //
                    break;
                case "Stop":
                    var stopped = await StopWorker(default).ConfigureAwait(false);
                    if (stopped) {
                        await SendStateUpdate().ConfigureAwait(false);
                    }
                    break;
                case "UpdateProviders":
                    // WithDiscardUnknownFields does currently not work, so we should fix this at source
                    var providerSettingsList = ProviderSettingsList.Parser.WithDiscardUnknownFields(true).ParseJson(sse.Data);
                    var providerSettings = providerSettingsList.ProviderSettings;
                    if (providerSettings != null) {
                        if (worker == null) {
                            _sessionConfig.SaveProviderSettings(providerSettings);
                        } else {
                            worker.UpdateProviders(providerSettings);
                        }
                        await SendStateUpdate().ConfigureAwait(false);
                    }
                    break;
                case "ApplyProcessingOptions":
                    var opts = JsonSerializer.Deserialize<ProcessingOptions>(sse.Data, _jsonOptions);
                    if (opts == null)
                        return;
                    if (worker == null) {
                        filterModel = SessionConfig.NoFilter;
                        filterResult = SessionWorker.TestFilter(opts.Filter);
                        if (filterResult.Diagnostics.Count == 0)
                            filterModel = opts.Filter;
                        _sessionConfig.SaveProcessingOptions(opts.BatchSize, opts.MaxWriteDelayMSecs, filterModel);
                    }
                    else {
                        filterResult = worker.ApplyProcessingOptions(opts.BatchSize, opts.MaxWriteDelayMSecs, opts.Filter);
                    }
                    await PostMessage($"Agent/ApplyFilterResult?eventId={sse.Id}", filterResult).ConfigureAwait(false);
                    await SendStateUpdate().ConfigureAwait(false);
                    break;
                case "TestFilter":
                    filterModel = JsonSerializer.Deserialize<FilterModel>(sse.Data, _jsonOptions);
                    filterResult = SessionWorker.TestFilter(filterModel);
                    await PostMessage($"Agent/TestFilterResult?eventId={sse.Id}", filterResult).ConfigureAwait(false);
                    break;
                case "UpdateEventSink":
                    var sinkProfile = JsonSerializer.Deserialize<EventSinkProfile>(sse.Data, _jsonOptions);
                    if (sinkProfile == null)
                        return;
                    if (worker == null) {
                        _sessionConfig.SaveSinkProfile(sinkProfile);
                    }
                    else {
                        await worker.UpdateEventSink(sinkProfile).ConfigureAwait(false);
                    }
                    await SendStateUpdate().ConfigureAwait(false);
                    break;
                default:
                    break;
            }
        }

        async Task PostMessage<T>(string path, T content) {
            var opts = _controlOptions.Value;
            var postUri = new Uri(opts.Uri, path);
            var httpMsg = new HttpRequestMessage(HttpMethod.Post, postUri);

            var mediaType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);
            httpMsg.Content = JsonContent.Create<T>(content, mediaType, _jsonOptions);

            var response = await _http.SendAsync(httpMsg).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        Task SendStateUpdate() {
            var ses = SessionWorker?.Session;
            //var agentName = _httpCertHandler.ClientCert.GetNameInfo(X509NameType.SimpleName, false);
            //var agentEmail = _httpCertHandler.ClientCert.GetNameInfo(X509NameType.EmailName, false);
            // 
            ImmutableList<EtwLogging.ProviderSetting> enabledProviders;
            var eventSinkState = new EventSinkState();

            eventSinkState.Profile = _sessionConfig.SinkProfile;

            var isRunning = _sessionWorkerAvailable != 0;
            if (isRunning && SessionWorker != null) {
                enabledProviders = ses?.EnabledProviders.ToImmutableList() ?? ImmutableList<EtwLogging.ProviderSetting>.Empty;
                eventSinkState.Error = SessionWorker.EventSinkError?.Message;
            }
            else {
                enabledProviders = _sessionConfig.Options.Providers.Select(provider => {
                    var setting = new ProviderSetting();
                    setting.Name = provider.Name;
                    setting.Level = (TraceEventLevel)provider.Level;
                    setting.MatchKeywords = provider.MatchKeywords;
                    return setting;
                }).ToImmutableList();
            }

            var state = new AgentState {
                EnabledProviders = enabledProviders,
                // Id = string.IsNullOrWhiteSpace(agentEmail) ? agentName : $"{agentName} ({agentEmail})",
                Id = string.Empty,  // will be filled in on server using the client certificate
                Host = Dns.GetHostName(),
                Site = _context.Configuration["Site"] ?? "<Undefined>",
                IsRunning = isRunning,
                IsStopped = !isRunning,
                EventSink = eventSinkState,
                ProcessingOptions = _sessionConfig.Options,
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

        public Task StartSSE() {
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

            this._eventSource = evt;

            // this Task will only terminate when the EventSource gets closed/disposed!
            return evt.StartAsync();
        }

        #endregion

        //TODO use async disposable service scope when in .NET 6.0;

        async Task<bool> StartWorker(CancellationToken cancelToken) {
            if (_sessionWorkerAvailable != 0)
                return false;

            var scope = _services.CreateScope();
            try {
                var oldScope = Interlocked.CompareExchange(ref this._workerScope, scope, null);
                if (oldScope != null) { // should not happen
                    oldScope.Dispose();
                    Interlocked.Exchange<IServiceScope?>(ref this._workerScope, null);
                    return false;
                }

                var sessionWorker = scope.ServiceProvider.GetRequiredService<SessionWorker>();
                // this returns the executing Task if it is already finished, or Task.CompletedTask
                var workerStartTask = sessionWorker.StartAsync(cancelToken);

                // if the new background service has already stopped, clean up and exit
                if (object.ReferenceEquals(workerStartTask, sessionWorker.ExecuteTask)) {
                    scope.Dispose();
                    return false;
                }

                // if we executing worker Task ends on its own (error?), clean up and update state
                _ = sessionWorker.ExecuteTask
                    .ContinueWith(swt => {
                        Interlocked.Exchange(ref _sessionWorkerAvailable, 0);
                        var oldScope = Interlocked.CompareExchange<IServiceScope?>(ref this._workerScope, null, scope);
                        oldScope?.Dispose();
                        Interlocked.CompareExchange(ref this._sessionWorker, null, sessionWorker);
                    }, TaskContinuationOptions.ExecuteSynchronously)
                    .ContinueWith(async swt => {
                        try {
                            if (swt.Exception != null) {
                                _logger.LogError(swt.Exception, "Session failure.");
                            }
                            await SendStateUpdate().ConfigureAwait(false);
                        }
                        catch (Exception ex) {
                            _logger.LogError(ex, "Error sending update to agent.");
                        }
                    });

                await workerStartTask.ConfigureAwait(false);

                this._sessionWorker = sessionWorker;
                Interlocked.Exchange(ref _sessionWorkerAvailable, 99);
                return true;
            }
            catch {
                scope.Dispose();
                throw;
            }
        }

        async Task<bool> StopWorker(CancellationToken cancelToken) {
            var oldWorkerAvailable = Interlocked.Exchange(ref _sessionWorkerAvailable, 0);
            if (oldWorkerAvailable == 0)
                return false;

            var oldWorker = Interlocked.Exchange(ref this._sessionWorker, null);
            if (oldWorker == null)  // should not happen 
                return false;
            await oldWorker.StopAsync(cancelToken).ConfigureAwait(false);

            // the continuation of oldWorker.ExecuteTask with clean up the scope
            return true;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            var sseTask = Task.CompletedTask;
            try {
                sseTask = StartSSE();

                stoppingToken.Register(async () => {
                    await StopWorker(default).ConfigureAwait(false);
                    var evt = _eventSource;
                    if (evt != null) {
                        _eventSource = null;
                        evt.Close();
                    }
                });
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Failure running service.");
            }

            try {
                await StartWorker(default).ConfigureAwait(false);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Failure starting session.");
            }

            try {
                await SendStateUpdate().ConfigureAwait(false);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error sending update to agent.");
            }

            // this task ends only when EventSource is shut down, e.g. calling EventSource.Close()
            await sseTask.ConfigureAwait(false);
        }

        public override void Dispose() {
            base.Dispose();
            _eventSource?.Dispose();
            _workerScope?.Dispose();
        }
    }
}
