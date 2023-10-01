using System.Collections.Immutable;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using KdSoft.EtwEvents.Server;
using KdSoft.EtwLogging;
using Microsoft.Extensions.Options;
using Gpb = Google.Protobuf;
using Kdfu = KdSoft.EtwEvents.FilterUtils;

namespace KdSoft.EtwEvents.PushAgent
{
    class ControlWorker: BackgroundService
    {
        readonly HostBuilderContext _context;
        readonly IServiceProvider _services;
        readonly SocketsHandlerCache _httpHandlerCache;
        readonly ControlConnector _controlConnector;
        readonly Channel<ControlEvent> _channel;
        readonly IOptionsMonitor<ControlOptions> _controlOptions;
        readonly SessionConfig _sessionConfig;
        readonly ILogger<ControlWorker> _logger;
        readonly JsonSerializerOptions _jsonOptions;
        readonly JsonFormatter _jsonFormatter;
        readonly string _stoppedFilePath;

        IServiceScope? _sessionScope;
        IDisposable? _controlOptionsListener;
        CancellationTokenRegistration _cancelRegistration;
        FilterSource? _emptyFilterSource;
        InstallCertResult _lastCertInstall = new InstallCertResult();

        SessionWorker? _sessionWorker;  // only valid when _sessionWorkerAvailable != 0
        int _sessionWorkerAvailable = 0;
        SessionWorker? SessionWorker => _sessionWorkerAvailable == 0 ? null : _sessionWorker!;

        static readonly byte[] _emptyBytes = new byte[0];

        public ControlWorker(
            HostBuilderContext context,
            IServiceProvider services,
            SocketsHandlerCache httpHandlerCache,
            ControlConnector controlConnector,
            Channel<ControlEvent> channel,
            IOptionsMonitor<ControlOptions> controlOptions,
            SessionConfig sessionConfig,
            ILogger<ControlWorker> logger
        ) {
            this._context = context;
            this._services = services;
            this._httpHandlerCache = httpHandlerCache;
            this._controlConnector = controlConnector;
            this._channel = channel;
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

            _stoppedFilePath = Path.Combine(_context.HostingEnvironment.ContentRootPath, ".stopped");
        }

        #region Control Channel

        async Task ProcessEvent(ControlEvent sse) {
            switch (sse.Event) {
                case Constants.StartEvent:
                    if (_sessionWorkerAvailable != 0) {
                        _logger.LogDebug("Session already starting.");
                        return;
                    }
                    File.Delete(_stoppedFilePath);
                    var started = await StartSessionWorker(default).ConfigureAwait(false);
                    await SendStateUpdate().ConfigureAwait(false);
                    return;

                case Constants.StopEvent:
                    if (_sessionWorkerAvailable == 0) {
                        _logger.LogDebug("Session already stopping.");
                        return;
                    }
                    break;

                case Constants.GetStateEvent:
                    await SendStateUpdate().ConfigureAwait(false);
                    return;

                default:
                    break;
            }

            // need different logic depending on whether a session is active or not
            var worker = _sessionWorkerAvailable == 0 ? null : SessionWorker;

            BuildFilterResult filterResult;

            switch (sse.Event) {
                case Constants.ResetEvent:
                    // simple way to create empty file
                    File.WriteAllBytes(_stoppedFilePath, _emptyBytes);
                    _ = await StopSessionWorker(default).ConfigureAwait(false);

                    var emptySettings = new Gpb.Collections.RepeatedField<ProviderSetting>();
                    _sessionConfig.SaveProviderSettings(emptySettings);

                    var emptyState = new ProcessingState();
                    _sessionConfig.SaveProcessingState(emptyState, true);

                    var emptySinks = new Gpb.Collections.MapField<string, EventSinkProfile>();
                    _sessionConfig.SaveSinkProfiles(emptySinks);

                    var emptyOptions = new LiveViewOptions();
                    _sessionConfig.SaveLiveViewOptions(emptyOptions);

                    await SendStateUpdate().ConfigureAwait(false);
                    break;

                case Constants.StopEvent:
                    // simple way to create empty file
                    File.WriteAllBytes(_stoppedFilePath, _emptyBytes);
                    var stopped = await StopSessionWorker(default).ConfigureAwait(false);
                    await SendStateUpdate().ConfigureAwait(false);
                    break;

                case Constants.SetEmptyFilterEvent:
                    var emptyFilter = string.IsNullOrEmpty(sse.Data) ? null : Filter.Parser.WithDiscardUnknownFields(true).ParseJson(sse.Data);
                    if (emptyFilter == null)
                        return;
                    _emptyFilterSource = Kdfu.BuildFilterSource(emptyFilter);
                    break;

                case Constants.TestFilterEvent:
                    // WithDiscardUnknownFields does currently not work, so we should fix this at source
                    var filter = string.IsNullOrEmpty(sse.Data) ? new Filter() : Filter.Parser.WithDiscardUnknownFields(true).ParseJson(sse.Data);
                    filterResult = SessionWorker.TestFilter(filter);
                    await PostProtoMessage($"Agent/TestFilterResult?eventId={sse.Id}", filterResult).ConfigureAwait(false);
                    break;

                //case Constants.CloseEventSinkEvent:
                //    var sinkName = sse.Data;
                //    if (sinkName == null)
                //        return;
                //    if (worker == null) {
                //        _sessionConfig.DeleteSinkProfile(sinkName);
                //    }
                //    else {
                //        await worker.CloseEventChannel(sinkName).ConfigureAwait(false);
                //    }
                //    await SendStateUpdate().ConfigureAwait(false);
                //    break;

                case Constants.StartLiveViewSinkEvent:
                    var managerSinkProfile = string.IsNullOrEmpty(sse.Data)
                        ? null
                        : EventSinkProfile.Parser.WithDiscardUnknownFields(true).ParseJson(sse.Data);
                    if (managerSinkProfile == null)
                        return;
                    if (worker != null) {
                        var retryStrategyFactory = () => new BackoffRetryStrategy(TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(1000), 10);
                        // make sure the manager sink name is the canonical name
                        managerSinkProfile.Name = Constants.LiveViewSinkName;
                        await worker.UpdateEventChannel(managerSinkProfile, retryStrategyFactory, false).ConfigureAwait(false);
                    }
                    await SendStateUpdate().ConfigureAwait(false);
                    break;

                case Constants.StopLiveViewSinkEvent:
                    if (worker != null) {
                        await worker.CloseEventChannel(Constants.LiveViewSinkName).ConfigureAwait(false);
                    }
                    await SendStateUpdate().ConfigureAwait(false);
                    break;

                case Constants.ApplyAgentOptionsEvent:
                    var agentOptions = string.IsNullOrEmpty(sse.Data) ? null : AgentOptions.Parser.WithDiscardUnknownFields(true).ParseJson(sse.Data);
                    if (agentOptions == null)
                        return;

                    var applyResult = new ApplyAgentOptionsResult();

                    if (agentOptions.HasEnabledProviders) {
                        var providerSettings = agentOptions.EnabledProviders;
                        if (worker == null) {
                            _sessionConfig.SaveProviderSettings(providerSettings);
                        }
                        else {
                            worker.UpdateProviders(providerSettings);
                        }
                    }

                    var processingOptions = agentOptions.ProcessingOptions;
                    if (processingOptions != null) {
                        // if we are not running, lets treat this like a filter test with saving
                        if (worker == null) {
                            filterResult = SessionWorker.TestFilter(processingOptions.Filter ?? new Filter());
                            var processingState = new ProcessingState {
                                FilterSource = filterResult.FilterSource,
                            };
                            var saveFilterSource = filterResult.Diagnostics.Count == 0; // also true when clearing filter
                            _sessionConfig.SaveProcessingState(processingState, saveFilterSource);
                        }
                        else {
                            filterResult = worker.ApplyProcessingOptions(processingOptions);
                        }
                        applyResult.FilterResult = filterResult;
                    }

                    if (agentOptions.HasEventSinkProfiles) {
                        var eventSinkProfiles = agentOptions.EventSinkProfiles;
                        if (worker == null) {
                            _sessionConfig.SaveSinkProfiles(eventSinkProfiles);
                        }
                        else {
                            await worker.UpdateEventChannels(eventSinkProfiles).ConfigureAwait(false);
                        }
                    }

                    var liveViewOptions = agentOptions.LiveViewOptions;
                    if (liveViewOptions != null) {
                        _sessionConfig.SaveLiveViewOptions(liveViewOptions);
                    }

                    await PostProtoMessage($"Agent/ApplyAgentOptionsResult?eventId={sse.Id}", applyResult).ConfigureAwait(false);
                    await SendStateUpdate().ConfigureAwait(false);
                    break;

                case Constants.InstallCertEvent:
                    if (string.IsNullOrEmpty(sse.Data))
                        return;
                    var installed = await InstallPemCertificate(sse.Data).ConfigureAwait(false);

                    // this could lead to infinite recursion if the installed certificate is not picked up on reconnect
                    await SendStateUpdate().ConfigureAwait(false);
                    break;

                default:
                    break;
            }
        }

        async Task<bool> InstallPemCertificate(string pemData) {
            bool success = false;
            var installTime = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow);
            try {
                var ephemeralCert = X509Certificate2.CreateFromPem(pemData, pemData);
                //NOTE: we can only import the private key as a persisted key if we export/import to Pkcs12.
                //      because X509Certificate2.CreateFromPem does not allow setting the KeyStorageFlags
                var cert = new X509Certificate2(ephemeralCert.Export(X509ContentType.Pkcs12, ""), "", X509KeyStorageFlags.PersistKeySet);
                var ch = new X509Chain { ChainPolicy = CertUtils.GetClientCertPolicy() };
                success = ch.Build(cert);
                if (!success) {
                    var sb = new StringBuilder();
                    foreach (var cst in ch.ChainStatus) {
                        sb.AppendLine($"{cst.Status}: {cst.StatusInformation}");
                    }
                    _lastCertInstall = new InstallCertResult {
                        Error = CertificateError.Invalid,
                        ErrorMessage = sb.ToString(),
                        Thumbprint = cert.Thumbprint,
                        InstallTime = installTime
                    };
                }
                else {
                    // Is the certificate already installed? If yes, don't reconnect.
                    var clientCerts = Utils.GetClientCertificates(_controlOptions.CurrentValue.ClientCertificate);
                    var certPrint = cert.Thumbprint.ToLower();
                    var alreadyInstalled = clientCerts.FindIndex(c => c.Thumbprint.ToLower() == certPrint) >= 0;

                    if (alreadyInstalled) {
                        _lastCertInstall = new InstallCertResult { Thumbprint = cert.Thumbprint, InstallTime = installTime };
                    }
                    else {
                        CertUtils.InstallMachineCertificate(cert);
                        clientCerts = Utils.GetClientCertificates(_controlOptions.CurrentValue.ClientCertificate);
                        success = clientCerts.FindIndex(c => c.Thumbprint.ToLower() == certPrint) >= 0;
                        if (success) {
                            // we need to use the new certificate everywhere
                            _httpHandlerCache.Refresh();
                            // we need to load and resave protected data with the new certificate
                            _sessionConfig.UpdateDataProtection(new DataCertOptions {
                                Location = StoreLocation.LocalMachine,
                                Thumbprint = certPrint
                            });
                            // and we need to restart the control connection
                            await _controlConnector.StartAsync(_controlOptions.CurrentValue, _cancelRegistration.Token).ConfigureAwait(false);
                            _lastCertInstall = new InstallCertResult { Thumbprint = cert.Thumbprint, InstallTime = installTime };
                        }
                        else {
                            _lastCertInstall = new InstallCertResult { Error = CertificateError.Install, Thumbprint = cert.Thumbprint, InstallTime = installTime };
                        }
                    }
                }
            }
            catch (CryptographicException cex) {
                success = false;
                _lastCertInstall = new InstallCertResult { Error = CertificateError.Crypto, ErrorMessage = cex.Message, InstallTime = installTime };
            }
            catch (Exception ex) {
                success = false;
                _lastCertInstall = new InstallCertResult { Error = CertificateError.Other, ErrorMessage = ex.Message, InstallTime = installTime };
            }
            return success;
        }

        async Task PostMessage(string path, HttpContent content) {
            var opts = _controlOptions.CurrentValue;
            var postUri = new Uri(opts.Uri, path);
            var httpMsg = new HttpRequestMessage(HttpMethod.Post, postUri) { Content = content };

            using var http = new HttpClient(_httpHandlerCache.Handler, false);
            var response = await http.SendAsync(httpMsg).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        Task PostJsonMessage<T>(string path, T content) {
            var mediaType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);
            var httpContent = JsonContent.Create(content, mediaType, _jsonOptions);
            return PostMessage(path, httpContent);
        }

        Task PostProtoMessage<T>(string path, T content) where T : IMessage<T> {
            var httpContent = new StringContent(_jsonFormatter.Format(content), Encoding.UTF8, MediaTypeNames.Application.Json);
            return PostMessage(path, httpContent);
        }

        Dictionary<string, EventSinkState> GetEventSinkStates() {
            var profiles = _sessionConfig.SinkProfiles;
            var result = new Dictionary<string, EventSinkState>();
            var failedSinks = SessionWorker?.FailedEventSinks ?? ImmutableDictionary<string, (string, Exception)>.Empty;
            var failedChannels = SessionWorker?.FailedEventChannels ?? ImmutableDictionary<string, EventChannel>.Empty;
            var activeChannels = SessionWorker?.ActiveEventChannels ?? ImmutableDictionary<string, EventChannel>.Empty;

            foreach (var profileEntry in profiles) {
                var profile = profileEntry.Value;
                var sinkStatus = new EventSinkStatus();
                if (failedSinks.TryGetValue(profile.Name, out var sink)) {
                    sinkStatus.LastError = sink.Item2.GetBaseException().Message;
                }
                else if (failedChannels.TryGetValue(profile.Name, out var failed)) {
                    var sinkError = failed.RunTask?.Exception?.GetBaseException().Message;
                    if (sinkError != null) {
                        sinkStatus.LastError = sinkError;
                    }
                }
                else if (activeChannels.TryGetValue(profile.Name, out var active)) {
                    if (active.SinkStatus != null) {
                        var status = active.SinkStatus.Status;
                        var sinkError = status.LastError?.GetBaseException().Message;
                        if (sinkError != null)
                            sinkStatus.LastError = sinkError;
                        if (status.NumRetries > 0)
                            sinkStatus.NumRetries = (uint)status.NumRetries;
                        // make sure access to status.RetryStartTicks is thread-safe
                        var retryStartTicks = Interlocked.Read(ref status.RetryStartTicks);
                        if (retryStartTicks != 0) {
                            sinkStatus.RetryStartTime = ProtoUtils.TimeStampFromUtcTicks(retryStartTicks);
                        }
                    }
                }
                var state = new EventSinkState { Profile = profile, Status = sinkStatus };
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
                enabledProviders = ses?.EnabledProviders.ToImmutableList() ?? ImmutableList<ProviderSetting>.Empty;
            }
            else {
                enabledProviders = _sessionConfig.State.ProviderSettings.ToImmutableList();
            }

            // fix up processingState with default FilterSource if missing, but don't affect the saved state
            var processingState = _sessionConfig.State.ProcessingState.Clone();
            if (processingState.FilterSource == null
                || processingState.FilterSource.TemplateVersion < (_emptyFilterSource?.TemplateVersion ?? 0))
                processingState.FilterSource = _emptyFilterSource;

            var clientCert = (_httpHandlerCache.Handler.SslOptions.ClientCertificates as X509Certificate2Collection)?.First();
            var clientCertLifeSpan = new Duration();
            if (clientCert != null) {
                var lifeSpan = clientCert.NotAfter - DateTime.Now;
                clientCertLifeSpan = Duration.FromTimeSpan(lifeSpan);
            }

            var state = new AgentState {
                EnabledProviders = { enabledProviders },
                // Id = string.IsNullOrWhiteSpace(agentEmail) ? agentName : $"{agentName} ({agentEmail})",
                Id = string.Empty,  // will be filled in on server using the client certificate
                Host = Dns.GetHostName(),
                Site = clientCert?.GetNameInfo(X509NameType.SimpleName, false) ?? "<Undefined>",
                ClientCertLifeSpan = clientCertLifeSpan,
                ClientCertThumbprint = clientCert?.Thumbprint,
                IsRunning = isRunning,
                IsStopped = !isRunning,
                EventSinks = { eventSinkStates },
                ProcessingState = processingState,
                LiveViewOptions = _sessionConfig.State.LiveViewOptions,
                LastCertInstall = _lastCertInstall
            };
            return PostProtoMessage("Agent/UpdateState", state);
        }

        async Task<bool> ProcessEvents(CancellationToken stoppingToken) {
            var finished = true;
            try {
                await foreach (var sse in _channel.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false)) {
                    try {
                        await ProcessEvent(sse).ConfigureAwait(false);
                        if (stoppingToken.IsCancellationRequested) {
                            finished = false;
                            break;
                        }
                    }
                    catch (Exception ex) {
                        _logger.LogError(ex, "Error processing control event.");
                    }
                }
            }
            catch (OperationCanceledException) {
                finished = false;
            }

            return finished;
        }

        #endregion

        #region Lifecycle

        async Task<bool> StartSessionWorker(CancellationToken cancelToken) {
            if (_sessionWorkerAvailable != 0)
                return false;

            var scope = _services.CreateScope();
            try {
                var oldScope = Interlocked.CompareExchange(ref _sessionScope, scope, null);
                if (oldScope != null) { // should not happen
                    oldScope.Dispose();
                    Interlocked.Exchange(ref _sessionScope, null);
                    return false;
                }

                var sessionWorker = scope.ServiceProvider.GetRequiredService<SessionWorker>();
                // this returns the executing Task if it is already finished, or Task.CompletedTask
                var workerStartTask = sessionWorker.StartAsync(cancelToken);

                // if the new background service has already stopped, clean up and exit
                if (ReferenceEquals(workerStartTask, sessionWorker.ExecuteTask)) {
                    scope.Dispose();
                    return false;
                }

                // if the executing worker Task ends on its own (error?), clean up and update state
                _ = sessionWorker.ExecuteTask?
                    .ContinueWith(swt => {
                        Interlocked.Exchange(ref _sessionWorkerAvailable, 0);
                        var oldScope = Interlocked.CompareExchange(ref _sessionScope, null, scope);
                        oldScope?.Dispose();
                        Interlocked.CompareExchange(ref _sessionWorker, null, sessionWorker);
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

        async Task<bool> StopSessionWorker(CancellationToken cancelToken) {
            var oldSessionWorkerAvailable = Interlocked.Exchange(ref _sessionWorkerAvailable, 0);
            if (oldSessionWorkerAvailable == 0)
                return false;

            var oldSessionWorker = Interlocked.Exchange(ref _sessionWorker, null);
            if (oldSessionWorker == null)  // should not happen 
                return false;

            // returns when oldSessionWorker.ExecuteAsync() returns
            await oldSessionWorker.StopAsync(cancelToken).ConfigureAwait(false);

            // the continuation of oldSessionWorker.ExecuteTask() will clean up the scope
            return true;
        }

        async Task ControlOptionsChanged(ControlOptions opts, CancellationToken stoppingToken) {
            try {
                if (!Equals(opts, _controlConnector.CurrentOptions)) {
                    await _controlConnector.StartAsync(opts, stoppingToken).ConfigureAwait(false);
                    _channel.Writer.TryWrite(ControlConnector.GetStateMessage);
                }
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error on {method}.", nameof(ControlOptionsChanged));
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            try {
                _cancelRegistration = stoppingToken.Register(async () => {
                    try {
                        await StopSessionWorker(default).ConfigureAwait(false);
                    }
                    catch (Exception ex) {
                        _logger.LogError(ex, "Failure stopping session.");
                    }
                });
                // start ControlConnector
                _controlOptionsListener = _controlOptions.OnChange(async opts => await ControlOptionsChanged(opts, stoppingToken).ConfigureAwait(false));
                await _controlConnector.StartAsync(_controlOptions.CurrentValue, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) {
                _cancelRegistration.Dispose();
                _controlOptionsListener?.Dispose();
                _controlConnector.Dispose();
                _logger.LogError(ex, "Failure running service.");
                return;
            }

            try {
                if (!File.Exists(_stoppedFilePath)) {
                    await StartSessionWorker(default).ConfigureAwait(false);
                }
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

            try {
                await ProcessEvents(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error processing control events.");
            }

            // this task ends only when the stoppingToken is triggered
            await _controlConnector.RunTask.ConfigureAwait(false);
        }

        public override void Dispose() {
            base.Dispose();
            _cancelRegistration.Dispose();
            _controlOptionsListener?.Dispose();
            _controlConnector?.Dispose();
            _sessionScope?.Dispose();
        }

        #endregion
    }
}
