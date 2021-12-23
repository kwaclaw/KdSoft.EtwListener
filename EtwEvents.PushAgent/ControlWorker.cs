using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using KdSoft.EtwEvents.Server;
using KdSoft.EtwLogging;
using LaunchDarkly.EventSource;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using pb = global::Google.Protobuf;

namespace KdSoft.EtwEvents.PushAgent
{
    class ControlWorker: BackgroundService
    {
        readonly HostBuilderContext _context;
        readonly IServiceProvider _services;
        readonly SocketsHttpHandler _httpHandler;
        readonly IOptions<ControlOptions> _controlOptions;
        readonly SessionConfig _sessionConfig;
        readonly ILogger<ControlWorker> _logger;
        readonly JsonSerializerOptions _jsonOptions;
        readonly JsonFormatter _jsonFormatter;

        EventSource? _eventSource;
        IServiceScope? _workerScope;

        FilterSource? _emptyFilterSource;

        SessionWorker? _sessionWorker;  // only valid when _sessionWorkerAvailable != 0
        int _sessionWorkerAvailable = 0;
        SessionWorker? SessionWorker => _sessionWorkerAvailable == 0 ? null : _sessionWorker!;

        public ControlWorker(
            HostBuilderContext context,
            IServiceProvider services,
            SocketsHttpHandler httpHandler,
            IOptions<ControlOptions> controlOptions,
            SessionConfig sessionConfig,
            ILogger<ControlWorker> logger
        ) {
            this._context = context;
            this._services = services;
            this._httpHandler = httpHandler;
            this._controlOptions = controlOptions;
            this._sessionConfig = sessionConfig;
            this._logger = logger;

            _jsonOptions = new JsonSerializerOptions {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                AllowTrailingCommas = true,
                WriteIndented = true
            };
            var jsonSettings = JsonFormatter.Settings.Default.WithFormatDefaultValues(true).WithFormatEnumsAsIntegers(true);
            _jsonFormatter = new JsonFormatter(jsonSettings);
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
            var worker = _sessionWorkerAvailable == 0 ? null : SessionWorker;

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
                case "SetEmptyFilter":
                    var emptyFilter = string.IsNullOrEmpty(sse.Data) ? null : Filter.Parser.WithDiscardUnknownFields(true).ParseJson(sse.Data);
                    if (emptyFilter == null)
                        return;
                    _emptyFilterSource = SessionWorker.BuildFilterSource(emptyFilter);
                    break;
                case "UpdateProviders":
                    // WithDiscardUnknownFields does currently not work, so we should fix this at source
                    var providerSettingsList = ProviderSettingsList.Parser.WithDiscardUnknownFields(true).ParseJson(sse.Data);
                    var providerSettings = providerSettingsList.ProviderSettings;
                    if (providerSettings != null) {
                        if (worker == null) {
                            _sessionConfig.SaveProviderSettings(providerSettings);
                        }
                        else {
                            worker.UpdateProviders(providerSettings);
                        }
                        await SendStateUpdate().ConfigureAwait(false);
                    }
                    break;
                case "ApplyProcessingOptions":
                    // WithDiscardUnknownFields does currently not work, so we should fix this at source
                    var processingOptions = string.IsNullOrEmpty(sse.Data) ? null : ProcessingOptions.Parser.WithDiscardUnknownFields(true).ParseJson(sse.Data);
                    if (processingOptions == null)
                        return;

                    filterResult = new BuildFilterResult();
                    // if we are not running, lets treat this like a filter test with saving
                    if (worker == null) {
                        filterResult = SessionWorker.TestFilter(processingOptions.Filter ?? new Filter());
                        var processingState = new ProcessingState {
                            FilterSource = filterResult.FilterSource,
                        };
                        bool saveFilterSource = filterResult.Diagnostics.Count == 0; // also true when clearing filter
                        _sessionConfig.SaveProcessingState(processingState, saveFilterSource);
                    }
                    else {
                        filterResult = worker.ApplyProcessingOptions(processingOptions);
                    }

                    await PostProtoMessage($"Agent/ApplyFilterResult?eventId={sse.Id}", filterResult).ConfigureAwait(false);
                    await SendStateUpdate().ConfigureAwait(false);
                    break;
                case "TestFilter":
                    // WithDiscardUnknownFields does currently not work, so we should fix this at source
                    var filter = string.IsNullOrEmpty(sse.Data) ? new Filter() : Filter.Parser.WithDiscardUnknownFields(true).ParseJson(sse.Data);
                    filterResult = SessionWorker.TestFilter(filter);
                    await PostProtoMessage($"Agent/TestFilterResult?eventId={sse.Id}", filterResult).ConfigureAwait(false);
                    break;
                case "UpdateEventSink":
                    var sinkProfile = string.IsNullOrEmpty(sse.Data) ? null : EventSinkProfile.Parser.WithDiscardUnknownFields(true).ParseJson(sse.Data);
                    if (sinkProfile == null)
                        return;
                    if (worker == null) {
                        _sessionConfig.SaveSinkProfile(sinkProfile);
                    }
                    else {
                        await worker.UpdateEventChannel(sinkProfile).ConfigureAwait(false);
                    }
                    await SendStateUpdate().ConfigureAwait(false);
                    break;
                case "CloseEventSink":
                    var sinkName = sse.Data;
                    if (sinkName == null)
                        return;
                    if (worker == null) {
                        _sessionConfig.DeleteSinkProfile(sinkName);
                    }
                    else {
                        await worker.CloseEventChannel(sinkName).ConfigureAwait(false);
                    }
                    await SendStateUpdate().ConfigureAwait(false);
                    break;
                default:
                    break;
            }
        }

        async Task PostMessage(string path, HttpContent content) {
            var opts = _controlOptions.Value;
            var postUri = new Uri(opts.Uri, path);
            var httpMsg = new HttpRequestMessage(HttpMethod.Post, postUri) { Content = content };

            using var http = new HttpClient(_httpHandler, false);
            var response = await http.SendAsync(httpMsg).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        Task PostJsonMessage<T>(string path, T content) {
            var mediaType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);
            var httpContent = JsonContent.Create<T>(content, mediaType, _jsonOptions);
            return PostMessage(path, httpContent);
        }

        Task PostProtoMessage<T>(string path, T content) where T : pb::IMessage<T> {
            var httpContent = new StringContent(_jsonFormatter.Format(content), Encoding.UTF8, MediaTypeNames.Application.Json);
            return PostMessage(path, httpContent);
        }

        Dictionary<string, EventSinkState> GetEventSinkStates() {
            var profiles = _sessionConfig.SinkProfiles;
            var result = new Dictionary<string, EventSinkState>();
            var failedChannels = SessionWorker?.FailedEventChannels ?? ImmutableDictionary<string, EventChannel>.Empty;

            foreach (var profileEntry in profiles) {
                Exception? sinkError = null;
                var profile = profileEntry.Value;
                if (failedChannels.TryGetValue(profile.Name, out var failed)) {
                    sinkError = failed.RunTask?.Exception;
                }
                var state = new EventSinkState { Profile = profile };
                if (sinkError != null)
                    state.Error = sinkError.GetBaseException().Message;
                result[profile.Name] = state;
            }
            return result;
        }

        Task SendStateUpdate() {
            var ses = SessionWorker?.Session;
            ImmutableList<ProviderSetting> enabledProviders;

            var eventSinkStates = GetEventSinkStates();

            var isRunning = _sessionWorkerAvailable != 0;
            if (isRunning && SessionWorker != null) {
                enabledProviders = ses?.EnabledProviders.ToImmutableList() ?? ImmutableList<EtwLogging.ProviderSetting>.Empty;
            }
            else {
                enabledProviders = _sessionConfig.State.ProviderSettings.ToImmutableList();
            }

            // fix up processingState with default FilterSource if missing, but don't affect the saved state
            var processingState = _sessionConfig.State.ProcessingState.Clone();
            if (processingState.FilterSource == null
                || processingState.FilterSource.TemplateVersion < (_emptyFilterSource?.TemplateVersion ?? 0))
                processingState.FilterSource = _emptyFilterSource;

            var clientCert = (_httpHandler.SslOptions.ClientCertificates as X509Certificate2Collection)?.First();

            var state = new AgentState {
                EnabledProviders = { enabledProviders },
                // Id = string.IsNullOrWhiteSpace(agentEmail) ? agentName : $"{agentName} ({agentEmail})",
                Id = string.Empty,  // will be filled in on server using the client certificate
                Host = Dns.GetHostName(),
                Site = clientCert?.GetNameInfo(X509NameType.SimpleName, false) ?? "<Undefined>",
                IsRunning = isRunning,
                IsStopped = !isRunning,
                EventSinks = { eventSinkStates },
                ProcessingState = processingState,
            };
            return PostProtoMessage("Agent/UpdateState", state);
        }

        async void EventReceived(object? sender, MessageReceivedEventArgs e) {
            try {
                var lastEventIdStr = string.IsNullOrEmpty(e.Message.LastEventId) ? "-1" : e.Message.LastEventId;
                var messageDataStr = string.IsNullOrEmpty(e.Message.Data) ? "<None>" : e.Message.Data;
                _logger?.LogInformation("{method}: {eventName}-{lastEventId}, {messageData}", nameof(EventReceived), e.EventName, lastEventIdStr, messageDataStr);
                await ProcessEvent(new ControlEvent { Event = e.EventName, Id = e.Message.LastEventId ?? "", Data = e.Message.Data ?? "" }).ConfigureAwait(false);
            }
            catch (Exception ex) {
                _logger?.LogError(ex, "Error in {method}.", nameof(EventReceived));
            }
        }

        void EventError(object? sender, ExceptionEventArgs e) {
            _logger?.LogError(e.Exception, "Error in EventSource.");
        }

        void EventSourceStateChanged(object? sender, StateChangedEventArgs e) {
            _logger?.LogInformation("{method}: {readyState}", nameof(EventSourceStateChanged), e.ReadyState);
        }

        public Task StartSSE() {
            var opts = _controlOptions.Value;
            var evtUri = new Uri(opts.Uri, "Agent/GetEvents");
            var cfgBuilder = Configuration.Builder(evtUri).HttpMessageHandler(_httpHandler);
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

            _eventSource = evt;

            // this Task will only terminate when the EventSource gets closed/disposed!
            return evt.StartAsync();
        }

        #endregion

        #region Lifecycle

        async Task<bool> StartWorker(CancellationToken cancelToken) {
            if (_sessionWorkerAvailable != 0)
                return false;

            var scope = _services.CreateScope();
            try {
                var oldScope = Interlocked.CompareExchange(ref _workerScope, scope, null);
                if (oldScope != null) { // should not happen
                    oldScope.Dispose();
                    Interlocked.Exchange<IServiceScope?>(ref _workerScope, null);
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

            var oldWorker = Interlocked.Exchange(ref _sessionWorker, null);
            if (oldWorker == null)  // should not happen 
                return false;
            await oldWorker.StopAsync(cancelToken).ConfigureAwait(false);

            // the continuation of oldWorker.ExecuteTask will clean up the scope
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

        #endregion
    }
}
