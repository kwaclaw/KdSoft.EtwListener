using System;
using System.Collections.Immutable;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KdSoft.EtwEvents.Client.Shared;
using KdSoft.EtwEvents.PushAgent.Models;
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
        readonly IOptions<ControlOptions> _controlOptions;
        readonly ILogger<ControlWorker> _logger;
        readonly HttpClient _http;
        readonly JsonSerializerOptions _jsonOptions;

        EventSource? _eventSource;
        IServiceScope? _workerScope;

        SessionWorker? _sessionWorker;  // only valid when _workerAvailable != 0
        int _workerAvailable = 0;
        SessionWorker? SessionWorker => _workerAvailable == 0 ? null : _sessionWorker!;

        public ControlWorker(
            HostBuilderContext context,
            IServiceProvider services,
            HttpClient http,
            IOptions<ControlOptions> controlOptions,
            ILogger<ControlWorker> logger
        ) {
            this._context = context;
            this._services = services;
            this._http = http;
            this._controlOptions = controlOptions;
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
                    if (_workerAvailable != 0) {
                        _logger?.LogInformation("Session already starting.");
                        return;
                    }
                    var started = await StartWorker(default).ConfigureAwait(false);
                    if (started) {
                        await SendStateUpdate().ConfigureAwait(false);
                    }
                    return;
                case "Stop":
                    if (_workerAvailable == 0) {
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

            // for the rest, SessionWorker must be running
            SessionWorker? worker;
            if (_workerAvailable == 0 || (worker = SessionWorker) == null) {
                _logger?.LogInformation("No session available for request.");
                return;
            }

            string? filter;
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
                        worker.UpdateProviders(providerSettings);
                        await SendStateUpdate().ConfigureAwait(false);
                    }
                    break;
                case "ApplyFilter":
                    var filterRequest = SetFilterRequest.Parser.ParseJson(sse.Data);
                    //var filterRequest = JsonSerializer.Deserialize<SetFilterRequest>(sse.Data, _jsonOptions);
                    filter = filterRequest?.CsharpFilter;
                    if (string.IsNullOrEmpty(filter))
                        return;
                    filterResult = worker.ApplyFilter(filter);
                    await PostMessage($"Agent/ApplyFilterResult?eventId={sse.Id}", filterResult).ConfigureAwait(false);
                    if (filterResult.Diagnostics.Count == 0) {
                        await SendStateUpdate().ConfigureAwait(false);
                    }
                    break;
                case "TestFilter":
                    var testRequest = TestFilterRequest.Parser.ParseJson(sse.Data);
                    //var testRequest = JsonSerializer.Deserialize<TestFilterRequest>(sse.Data, _jsonOptions);
                    filter = testRequest?.CsharpFilter;
                    if (string.IsNullOrEmpty(filter))
                        return;
                    filterResult = worker.TestFilter(filter);
                    await PostMessage($"Agent/TestFilterResult?eventId={sse.Id}", filterResult).ConfigureAwait(false);
                    break;
                case "UpdateEventSink":
                    var sinkProfile = JsonSerializer.Deserialize<EventSinkProfile>(sse.Data, _jsonOptions);
                    if (sinkProfile == null)
                        return;
                    await worker.UpdateEventSink(sinkProfile).ConfigureAwait(false);
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
            var isRunning = _workerAvailable != 0;
            var eventSinkState = new EventSinkState();
            if (isRunning && SessionWorker != null) {
                eventSinkState.Profile = SessionWorker.EventSinkProfile;
                eventSinkState.Error = SessionWorker.EventSinkError?.Message;
            }
            var state = new Models.AgentState {
                EnabledProviders = ses?.EnabledProviders.ToImmutableList() ?? ImmutableList<EtwLogging.ProviderSetting>.Empty,
                // Id = string.IsNullOrWhiteSpace(agentEmail) ? agentName : $"{agentName} ({agentEmail})",
                Id = string.Empty,  // will be filled in on server using the client certificate
                Host = Dns.GetHostName(),
                Site = _context.Configuration["Site"],
                FilterBody = ses?.GetCurrentFilterBody(),
                IsRunning = isRunning,
                IsStopped = !isRunning,
                EventSink = eventSinkState,
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
            if (_workerAvailable != 0)
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
                var workerStartTask = sessionWorker.StartAsync(cancelToken);

                // if the new background service has already stopped, clean up and exit
                if (object.ReferenceEquals(workerStartTask, sessionWorker.ExecuteTask)) {
                    scope.Dispose();
                    return false;
                }

                // if we executing worker Task ends on its own (error?), clean up and update state
                _ = sessionWorker.ExecuteTask
                    .ContinueWith(swt => {
                        Interlocked.Exchange(ref _workerAvailable, 0);
                        var oldScope = Interlocked.CompareExchange<IServiceScope?>(ref this._workerScope, null, scope);
                        oldScope?.Dispose();
                        Interlocked.CompareExchange(ref this._sessionWorker, null, sessionWorker);
                    }, TaskContinuationOptions.ExecuteSynchronously)
                    .ContinueWith(t => SendStateUpdate());

                await workerStartTask.ConfigureAwait(false);

                this._sessionWorker = sessionWorker;
                Interlocked.Exchange(ref _workerAvailable, 99);
                return true;
            }
            catch {
                scope.Dispose();
                throw;
            }
        }

        async Task<bool> StopWorker(CancellationToken cancelToken) {
            var oldWorkerAvailable = Interlocked.Exchange(ref _workerAvailable, 0);
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

                await StartWorker(default).ConfigureAwait(false);
                await SendStateUpdate().ConfigureAwait(false);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Failure running service.");
            }
            await sseTask.ConfigureAwait(false);
        }

        public override void Dispose() {
            base.Dispose();
            _http?.Dispose();
            _eventSource?.Dispose();
            _workerScope?.Dispose();
        }
    }
}
