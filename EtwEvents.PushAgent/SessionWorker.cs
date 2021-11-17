using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.Collections;
using KdSoft.EtwEvents.Client;
using KdSoft.EtwEvents.Server;
using KdSoft.EtwLogging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using cat = Microsoft.CodeAnalysis.Text;

namespace KdSoft.EtwEvents.PushAgent
{
    class SessionWorker: BackgroundService
    {
        readonly SessionConfig _sessionConfig;
        readonly IOptions<EventQueueOptions> _eventQueueOptions;
        readonly EventSinkService _sinkService;
        readonly IConfiguration _config;
        readonly ILoggerFactory _loggerFactory;

        readonly ILogger<SessionWorker> _logger;
        readonly EventSinkHolder _sinkHolder;
        readonly JsonSerializerOptions _jsonOptions;

        public SessionConfig SessionConfig => _sessionConfig;

        RealTimeTraceSession? _session;
        public RealTimeTraceSession? Session => _session;

        PersistentEventProcessor? _processor;

        public Exception? EventSinkError => _sinkHolder.FailedEventSinks.FirstOrDefault().Value.error;

        public SessionWorker(
            SessionConfig sessionConfig,
            IOptions<EventQueueOptions> eventQueueOptions,
            EventSinkService sinkService,
            IConfiguration config,
            ILoggerFactory loggerFactory
        ) {
            this._sessionConfig = sessionConfig;
            this._eventQueueOptions = eventQueueOptions;
            this._sinkService = sinkService;
            this._config = config;
            this._loggerFactory = loggerFactory;
            
            _logger = loggerFactory.CreateLogger<SessionWorker>();
            _sinkHolder = new EventSinkHolder();
            _jsonOptions = new JsonSerializerOptions {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                AllowTrailingCommas = true,
                WriteIndented = true
            };
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

        public static (cat.SourceText source, IReadOnlyList<cat.TextChangeRange> partRanges) BuildSource(string filterTemplate, params string[] filterParts) {
            int indx = 0;
            var markers = filterParts.Select(p => $"\u001D{indx++}").ToArray();
            var initSource = string.Format(CultureInfo.InvariantCulture, filterTemplate, markers);
            var initSourceText = cat.SourceText.From(initSource);
            var partChanges = new cat.TextChange[4];
            for (indx = 0; indx < 4; indx++) {
                var part = filterParts[indx] ?? String.Empty;
                int insertionIndex = initSource.IndexOf(markers[indx], StringComparison.Ordinal);
                partChanges[indx] = new cat.TextChange(new cat.TextSpan(insertionIndex, 2), part);
            }
            var changedSourceText = initSourceText.WithChanges(partChanges);
            var ranges = changedSourceText.GetChangeRanges(initSourceText);
            return (changedSourceText, ranges);
        }

        public static (cat.SourceText? source, IReadOnlyList<cat.TextChangeRange>? partRanges) BuildFilterSource(FilterModel? filterModel) {
            (cat.SourceText? source, IReadOnlyList<cat.TextChangeRange>? partRanges) result;
            var parts = filterModel?.FilterParts;
            var template = filterModel?.FilterTemplate;
            if (parts == null || parts.Length == 0 || string.IsNullOrWhiteSpace(parts[parts.Length - 1]))
                result = (null, null);
            else if (string.IsNullOrWhiteSpace(template))
                result = (null, null);
            else
                result = BuildSource(template, parts);
            return result;
        }

        public static IReadOnlyList<cat.LinePositionSpan> GetPartLineSpans(cat.SourceText sourceText, IReadOnlyList<cat.TextChangeRange> partRanges) {
            var result = new cat.LinePositionSpan[partRanges.Count];
            int offset = 0;
            var lines = sourceText.Lines;
            for (int indx = 0; indx < partRanges.Count; indx++) {
                var newLen = partRanges[indx].NewLength;
                var span = new cat.TextSpan(partRanges[indx].Span.Start + offset, newLen);
                result[indx] = lines.GetLinePositionSpan(span);

                offset += newLen - 2;
            }
            return result;
        }

        public static BuildFilterResult TestFilter(FilterModel? filterModel) {
            var result = new BuildFilterResult();

            var filterSource = BuildFilterSource(filterModel);
            if (filterSource.source == null) {
                result.NoFilter = true;
            }
            else {
                var diagnostics = RealTimeTraceSession.TestFilter(filterSource.source);
                result.AddDiagnostics(diagnostics);
                result.AddSourceLines(filterSource.source.Lines);
                var partLineSpans = GetPartLineSpans(filterSource.source, filterSource.partRanges!);
                result.AddPartLineSpans(partLineSpans);
            }

            return result;
        }

        public BuildFilterResult ApplyProcessingOptions(int batchSize, int maxWriteDelay, FilterModel? filterModel) {
            var ses = _session;
            if (ses == null)
                throw new InvalidOperationException("No trace session active.");
            var result = new BuildFilterResult();

            if (object.ReferenceEquals(filterModel, SessionConfig.ClearFilter)) {
                // clear filter
                ses.SetFilter(null, _config);
            }
            else {
                var filterSource = BuildFilterSource(filterModel);
                if (filterSource.source == null) {
                    filterModel = SessionConfig.NoFilter;
                    result.NoFilter = true;
                }
                else {
                    var diagnostics = ses.SetFilter(filterSource.source, _config);
                    if (diagnostics.Length > 0) {
                        filterModel = SessionConfig.NoFilter;
                    }
                    result.AddDiagnostics(diagnostics);
                    result.AddSourceLines(filterSource.source.Lines);
                    var partLineSpans = GetPartLineSpans(filterSource.source, filterSource.partRanges!);
                    result.AddPartLineSpans(partLineSpans);
                }
            }

            var oldBatchSize = _processor?.ChangeBatchSize(batchSize) ?? -1;
            var oldMaxWriteDelay = _processor?.ChangeWriteDelay(maxWriteDelay) ?? -1;

            _sessionConfig.SaveProcessingOptions(batchSize, maxWriteDelay, filterModel!);
            return result;
        }

        #endregion

        #region Event Sink

        async Task<IEventSinkFactory?> LoadSinkFactory(string sinkType, string version) {
            // One can initiate unloading of the CollectibleAssemblyLoadContext by either calling its Unload method
            // getting rid of the reference to the AssemblyLoadContext, e.g. by just using a local variable;
            // see https://docs.microsoft.com/en-us/dotnet/standard/assembly/unloadability
            var loadContext = new CollectibleAssemblyLoadContext();
            var sinkFactory = _sinkService.LoadEventSinkFactory(sinkType, version, loadContext);
            if (sinkFactory == null) {
                _logger.LogInformation($"Downloading event sink factory '{sinkType}~{version}'.");
                await _sinkService.DownloadEventSink(sinkType, version);
            }
            sinkFactory = _sinkService.LoadEventSinkFactory(sinkType, version, loadContext);
            return sinkFactory;
        }

        async Task<IEventSink> CreateEventSink(EventSinkProfile sinkProfile) {
            var optsJson = JsonSerializer.Serialize(sinkProfile.Options, _jsonOptions);
            var credsJson = JsonSerializer.Serialize(sinkProfile.Credentials, _jsonOptions);
            var sinkFactory = await LoadSinkFactory(sinkProfile.SinkType, sinkProfile.Version).ConfigureAwait(false);
            if (sinkFactory == null)
                throw new InvalidOperationException($"Error loading event sink factory '{sinkProfile.SinkType}~{sinkProfile.Version}'.");
            var logger = _loggerFactory.CreateLogger(sinkProfile.SinkType);
            return await sinkFactory.Create(optsJson, credsJson, logger).ConfigureAwait(false);
        }

        Task ConfigureEventSinkClosure(string name, IEventSink sink) {
            return sink.RunTask.ContinueWith(async rt => {
                if (rt.Exception != null) {
                    await _sinkHolder.HandleFailedEventSink(name, sink, rt.Exception).ConfigureAwait(false);
                }
                else {
                    await _sinkHolder.CloseEventSink(name, sink).ConfigureAwait(false);
                }
            }, TaskScheduler.Default);
        }

        async Task CloseEventSinks() {
            var (activeSinks, failedSinks) = _sinkHolder.ClearEventSinks();
            var disposeEntries = activeSinks.Select(sink => (sink.Key, _sinkHolder.CloseEventSink(sink.Key, sink.Value))).ToArray();
            foreach (var disposeEntry in disposeEntries) {
                try {
                    await disposeEntry.Item2.ConfigureAwait(false);
                }
                catch (Exception ex) {
                    _logger.LogError(ex, $"Error closing event sink '{disposeEntry.Item1}'.");
                }
            }
        }

        public async Task UpdateEventSink(EventSinkProfile sinkProfile) {
            // since we only have one event sink we can close them all
            await CloseEventSinks().ConfigureAwait(false);

            var sink = await CreateEventSink(sinkProfile).ConfigureAwait(false);
            try {
                _sinkHolder.AddEventSink(sinkProfile.Name, sink);
                var closureTask = ConfigureEventSinkClosure(sinkProfile.Name, sink);
                _sessionConfig.SaveSinkProfile(sinkProfile);
            }
            catch (Exception ex) {
                await sink.DisposeAsync().ConfigureAwait(false);
                _logger.LogError(ex, $"Error updating event sink '{sinkProfile.Name}'.");
                throw;
            }
        }

        #endregion

        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            try {
                if (!_sessionConfig.OptionsAvailable) {
                    _logger.LogInformation("Starting session without configured options.");
                }

                var logger = _loggerFactory.CreateLogger<RealTimeTraceSession>();
                var session = new RealTimeTraceSession("default", TimeSpan.MaxValue, logger, false);
                this._session = session;

                stoppingToken.Register(() => {
                    var ses = _session;
                    if (ses != null) {
                        _session = null;
                        ses.Dispose();
                    }
                });

                session.GetLifeCycle().Used();

                var filterSource = BuildFilterSource(_sessionConfig.Options.Filter);
                if (filterSource.source != null) {
                    var diagnostics = session.SetFilter(filterSource.source, _config);
                    if (!diagnostics.IsEmpty) {
                        logger.LogError($"Filter compilation failed.\n{diagnostics}");
                    }
                }

                // enable the providers
                foreach (var provider in _sessionConfig.Options.Providers) {
                    var setting = new ProviderSetting();
                    setting.Name = provider.Name;
                    setting.Level = (TraceEventLevel)provider.Level;
                    setting.MatchKeywords = provider.MatchKeywords;
                    session.EnableProvider(setting);
                }

                if (_sessionConfig.SinkProfileAvailable) {
                    await UpdateEventSink(_sessionConfig.SinkProfile!).ConfigureAwait(false);
                }
                try {
                    long sequenceNo = 0;
                    WriteBatchAsync writeBatch = async (batch) => {
                        bool success = await _sinkHolder.ProcessEventBatch(batch, sequenceNo).ConfigureAwait(false);
                        if (success) {
                            sequenceNo += batch.Events.Count;
                        }
                        return success;
                    };

                    var processorLogger = _loggerFactory.CreateLogger<PersistentEventProcessor>();
                    using (var processor = new PersistentEventProcessor(
                        writeBatch,
                        _eventQueueOptions.Value.FilePath,
                        processorLogger,
                        _sessionConfig.Options.BatchSize,
                        _sessionConfig.Options.MaxWriteDelayMSecs)
                    ) {
                        this._processor = processor;
                        _logger.LogInformation("SessionWorker started.");
                        await processor.Process(session, stoppingToken).ConfigureAwait(false);
                    }
                }
                finally {
                    this._processor = null;
                    await CloseEventSinks().ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) {
                _logger.LogInformation("SessionWorker stopped.");
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Session failure.");
            }
        }

        public override void Dispose() {
            base.Dispose();
            _session?.Dispose();
        }
    }
}
