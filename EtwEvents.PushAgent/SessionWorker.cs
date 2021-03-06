using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Google.Protobuf.Collections;
using KdSoft.EtwEvents.Server;
using KdSoft.EtwLogging;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using fu = KdSoft.EtwEvents.FilterUtils;

namespace KdSoft.EtwEvents.PushAgent
{
    class SessionWorker: BackgroundService
    {
        readonly SessionConfig _sessionConfig;
        readonly IOptions<EventQueueOptions> _eventQueueOptions;
        readonly SocketsHttpHandler _httpHandler;
        readonly Channel<ControlEvent> _controlChannel;
        readonly EventSinkService _sinkService;
        readonly IConfiguration _config;
        readonly ILoggerFactory _loggerFactory;

        readonly ILogger<SessionWorker> _logger;
        readonly EventProcessor _eventProcessor;
        readonly JsonSerializerOptions _jsonOptions;

        static readonly IRetryStrategy _defaultRetryStrategy = new BackoffRetryStrategy(
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromHours(2),
            forever: true
        );

        public SessionConfig SessionConfig => _sessionConfig;

        RealTimeTraceSession? _session;
        public RealTimeTraceSession? Session => _session;

        public SessionWorker(
            SessionConfig sessionConfig,
            IOptions<EventQueueOptions> eventQueueOptions,
            SocketsHttpHandler httpHandler,
            Channel<ControlEvent> controlChannel,
            EventSinkService sinkService,
            IConfiguration config,
            ILoggerFactory loggerFactory
        ) {
            this._sessionConfig = sessionConfig;
            this._eventQueueOptions = eventQueueOptions;
            this._httpHandler = httpHandler;
            this._controlChannel = controlChannel;
            this._sinkService = sinkService;
            this._config = config;
            this._loggerFactory = loggerFactory;

            _logger = loggerFactory.CreateLogger<SessionWorker>();
            _eventProcessor = new EventProcessor();
            _jsonOptions = new JsonSerializerOptions {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                AllowTrailingCommas = true,
                WriteIndented = true
            };
        }

        string GetSiteName() {
            var clientCert = (_httpHandler.SslOptions.ClientCertificates as X509Certificate2Collection)?.First();
            return clientCert?.GetNameInfo(X509NameType.SimpleName, false) ?? "<Undefined>";
        }

        #region Provider Settings

        public void UpdateProviders(RepeatedField<ProviderSetting> providerSettings) {
            var ses = _session;
            if (ses == null)
                throw new InvalidOperationException("No trace session active.");
            var providersToBeDisabled = ses.EnabledProviders.Select(ep => ep.Name).ToHashSet();
            foreach (var setting in providerSettings) {
                ses.EnableProvider(setting);
                providersToBeDisabled.Remove(setting.Name);
            }
            foreach (var providerName in providersToBeDisabled) {
                ses.DisableProvider(providerName);
            }
            _sessionConfig.SaveProviderSettings(providerSettings);
        }

        #endregion

        #region Processing

        public static BuildFilterResult TestFilter(Filter filter) {
            var result = new BuildFilterResult();

            // an empty filter is OK
            if (filter.FilterParts.Count > 0) {
                var (sourceText, dynamicRanges) = fu.BuildSourceText(filter);
                if (sourceText == null) {
                    var diagnostic = Diagnostic.Create(
                        "FL1000", "Filter", "Input filter not well formed.", DiagnosticSeverity.Error, DiagnosticSeverity.Error, true, 0
                    );
                    result.AddDiagnostics(ImmutableArray<Diagnostic>.Empty.Add(diagnostic));
                }
                else {
                    var diagnostics = RealTimeTraceSession.TestFilter(sourceText);
                    result.AddDiagnostics(diagnostics);
                    result.FilterSource = fu.BuildFilterSource(sourceText, dynamicRanges!, filter);
                }
            }

            return result;
        }

        public BuildFilterResult ApplyProcessingOptions(ProcessingOptions options) {
            var ses = _session;
            if (ses == null)
                throw new InvalidOperationException("No trace session active.");
            var result = new BuildFilterResult();

            if (options.Filter.FilterParts.Count > 0) {
                var (sourceText, dynamicRanges) = fu.BuildSourceText(options.Filter);
                if (sourceText == null) {
                    var diagnostic = Diagnostic.Create(
                        "FL1000", "Filter", "Input filter not well formed.", DiagnosticSeverity.Error, DiagnosticSeverity.Error, true, 0
                    );
                    result.AddDiagnostics(ImmutableArray<Diagnostic>.Empty.Add(diagnostic));
                }
                else {
                    var diagnostics = ses.SetFilter(sourceText, _config);
                    result.AddDiagnostics(diagnostics);
                    result.FilterSource = fu.BuildFilterSource(sourceText, dynamicRanges!, options.Filter);
                }
            }
            else {
                // clear filter
                ses.SetFilter(null, _config);
            }

            var processingState = new ProcessingState {
                FilterSource = result.FilterSource,
            };
            bool saveFilterSource = result.Diagnostics.Count == 0;  // also true when clearing filter
            _sessionConfig.SaveProcessingState(processingState, saveFilterSource);
            return result;
        }

        #endregion

        #region Event Sink

        public IImmutableDictionary<string, EventChannel> ActiveEventChannels => _eventProcessor.ActiveEventChannels;
        public IImmutableDictionary<string, EventChannel> FailedEventChannels => _eventProcessor.FailedEventChannels;

        public async Task<bool> CloseEventChannel(string name) {
            if (_eventProcessor.ActiveEventChannels.TryGetValue(name, out var channel)) {
                await channel.DisposeAsync().ConfigureAwait(false);
                return true;
            }
            _sessionConfig.DeleteSinkProfile(name);
            return false;
        }

        /// <summary>
        /// Updates or re-creates EventChannel based on an <see cref="EventSinkProfile"/>.
        /// </summary>
        /// <param name="sinkProfile"><see cref="EventSinkProfile"/> to use for event channnel.</param>
        /// <param name="retryStrategy"><see cref="IRetryStrategy"/> to use for event channnel.</param>
        /// <param name="isPersistent">Indicates if the event sink profile is persistent and must be saved.</param>
        public async Task UpdateEventChannel(EventSinkProfile sinkProfile, IRetryStrategy? retryStrategy = null, bool isPersistent = true) {
            if (_eventProcessor.ActiveEventChannels.TryGetValue(sinkProfile.Name, out var channel)) {
                var profileIsStored = _sessionConfig.SinkProfiles.TryGetValue(sinkProfile.Name, out var storedProfile);
                // the only settings we can update on a running channel/EventSink are BatchSize and MaxWriteDelayMSecs
                if (!profileIsStored || EventSinkProfile.Matches(sinkProfile, storedProfile!)) {
                    var batchSize = sinkProfile.BatchSize == 0 ? 100 : sinkProfile.BatchSize;
                    var maxWriteDelayMSecs = sinkProfile.MaxWriteDelayMSecs == 0 ? 400 : sinkProfile.MaxWriteDelayMSecs;
                    channel.ChangeBatchSize(batchSize);
                    channel.ChangeWriteDelay(maxWriteDelayMSecs);
                    if (isPersistent) {
                        _sessionConfig.SaveSinkProfile(sinkProfile);
                    }
                    return;
                }
                else {
                    // the channel is active, but we need to re-create it as it does not match the new profile
                    await channel.DisposeAsync().ConfigureAwait(false);
                }
            }

            EventChannel CreateChannel(IEventSink eventSink) {
                var batchSize = sinkProfile.BatchSize == 0 ? 100 : sinkProfile.BatchSize;
                var maxWriteDelayMSecs = sinkProfile.MaxWriteDelayMSecs == 0 ? 400 : sinkProfile.MaxWriteDelayMSecs;

                if (sinkProfile.PersistentChannel) {
                    var persistentLogger = _loggerFactory.CreateLogger<PersistentEventChannel>();
                    // EventQueue subdirectory is named after sink profile's name
                    var eventQeueDir = Path.Combine(_eventQueueOptions.Value.BaseDirectory, sinkProfile.Name);
                    Directory.CreateDirectory(eventQeueDir);  // make sure it exists
                    var filePath = Path.Combine(eventQeueDir, _eventQueueOptions.Value.FileName);
                    return PersistentEventChannel.Create(eventSink, persistentLogger, filePath, batchSize, maxWriteDelayMSecs);
                }
                else {
                    var transientLogger = _loggerFactory.CreateLogger<TransientEventChannel>();
                    return TransientEventChannel.Create(eventSink, transientLogger, batchSize, maxWriteDelayMSecs);
                }
            }

            EventChannel? newChannel = null;
            var sinkProxy = await sinkProfile.CreateRetryProxy(
                _sinkService, retryStrategy ?? _defaultRetryStrategy, GetSiteName(), _loggerFactory).ConfigureAwait(false);
            try {
                newChannel = _eventProcessor.AddChannel(sinkProfile.Name, sinkProxy, CreateChannel);
                if (isPersistent) {
                    _sessionConfig.SaveSinkProfile(sinkProfile);
                }
                sinkProxy.Changed += () => {
                    if (_eventProcessor.ActiveEventChannels.ContainsKey(sinkProfile.Name))   {
                        var couldWrite = _controlChannel.Writer.TryWrite(ControlConnector.GetStateMessage);
                        if (!couldWrite) {
                            _logger?.LogError("Error in {method}. Could not write event {event} to control channel.", "Changed Handler", ControlConnector.GetStateMessage.Event);
                        }
                    }
                };
            }
            catch (Exception ex) {
                if (newChannel == null) {
                    await sinkProxy.DisposeAsync().ConfigureAwait(false);
                }
                else {
                    await newChannel.DisposeAsync().ConfigureAwait(false);
                }
                _logger.LogError(ex, "Error updating event sink '{eventSink}'.", sinkProfile.Name);
                throw;
            }
        }

        public async Task UpdateEventChannels(IDictionary<string, EventSinkProfile> sinkProfiles) {
            var confNames = _sessionConfig.SinkProfiles.Keys.ToImmutableHashSet(StringComparer.CurrentCultureIgnoreCase);
            var namesToRemove = confNames.Except(sinkProfiles.Keys);
            foreach (var toRemove in namesToRemove) {
                await CloseEventChannel(toRemove).ConfigureAwait(false);
            }
            foreach (var profileEntry in sinkProfiles) {
                await UpdateEventChannel(profileEntry.Value).ConfigureAwait(false);
            }
        }

        #endregion

        // Note: the stoppingToken gets cancelled by calling BackgroundService.StopAsync() or BackgroundService.Dispose()
        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            try {
                if (!_sessionConfig.StateAvailable) {
                    _logger.LogInformation("Starting session without configured options.");
                }

                var logger = _loggerFactory.CreateLogger<RealTimeTraceSession>();
                var sessionName = Constants.TraceSessionNamePrefix + "_" + GetSiteName();
                var session = new RealTimeTraceSession(sessionName, TimeSpan.MaxValue, logger, false);
                this._session = session;

                stoppingToken.Register(() => {
                    var ses = _session;
                    if (ses != null) {
                        _session = null;
                        ses.Dispose();
                    }
                });

                session.GetLifeCycle().Used();

                // load filter from session configuration storage and apply/set filter
                var filterSource = _sessionConfig.State.ProcessingState?.FilterSource;
                if (filterSource != null) {
                    var filter = string.Join(Environment.NewLine, filterSource.SourceLines.Select(sl => sl.Text ?? ""));
                    var diagnostics = session.SetFilter(SourceText.From(filter), _config);
                    if (!diagnostics.IsEmpty) {
                        var diagnosticsStr = string.Join("\n\t", diagnostics.Select(dg => dg.ToString()).ToArray());
                        logger.LogError("Filter compilation failed.\n\t{diagnostics}", diagnosticsStr);
                    }
                }

                // enable the providers
                foreach (var setting in _sessionConfig.State.ProviderSettings) {
                    session.EnableProvider(setting);
                }

                try {
                    foreach (var profileEntry in _sessionConfig.SinkProfiles) {
                        await UpdateEventChannel(profileEntry.Value).ConfigureAwait(false);
                    }
                }
                catch {
                    // exception already logged
                }

                var processingTask = _eventProcessor.Process(session, stoppingToken);
                _logger.LogInformation("SessionWorker started.");
                await processingTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                _logger.LogInformation("SessionWorker stopped.");
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Session failure.");
            }
            finally {
                await _eventProcessor.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
