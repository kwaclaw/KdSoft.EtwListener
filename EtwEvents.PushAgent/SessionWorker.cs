using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
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

        public static (string? source, List<string> markers) BuildTemplateSource(Filter filter) {
            var sb = new StringBuilder();
            var markers = new List<string>();
            int indx = 0;
            foreach (var filterPart in filter.FilterParts) {
                var partName = filterPart.Name?.Trim();
                if (string.IsNullOrEmpty(partName)) {
                    return (null, markers);
                }
                // if the main method body is empty then we have no filter
                if (string.Equals(partName, "method", StringComparison.OrdinalIgnoreCase)) {
                    if (filterPart.Lines.Count == 0)
                        return (null, markers);
                }
                if (string.Equals(partName, "template", StringComparison.OrdinalIgnoreCase)) {
                    foreach (var line in filterPart.Lines)
                        sb.AppendLine(line);
                }
                else {
                    var marker = $"\u001D{indx++}";
                    sb.AppendLine(marker);
                    markers.Add(marker);
                }
            }
            return (sb.ToString(), markers);
        }

        public static List<cat.TextChange> BuildSourceChanges(string initSource, IList<string> markers, Filter filter) {
            var sb = new StringBuilder();
            int indx = 0;
            var partChanges = new List<cat.TextChange>(markers.Count);
            foreach (var filterPart in filter.FilterParts) {
                var partName = filterPart.Name?.Trim();
                if (string.IsNullOrEmpty(partName) || string.Equals(partName, "Template", StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }
                int insertionIndex = initSource.IndexOf(markers[indx++], StringComparison.Ordinal);
                sb.Clear();
                foreach (var line in filterPart.Lines)
                    sb.AppendLine(line);
                partChanges.Add(new cat.TextChange(new cat.TextSpan(insertionIndex, 2), sb.ToString()));
            }
            return partChanges;
        }

        public static (cat.SourceText? sourceText, IReadOnlyList<cat.TextChangeRange>? partRanges) BuildFilterSource(Filter filter) {
            (cat.SourceText? sourceText, IReadOnlyList<cat.TextChangeRange>? partRanges) result;

            var (templateSource, markers) = BuildTemplateSource(filter);
            if (templateSource == null)
                return (null, null);

            var partChanges = BuildSourceChanges(templateSource, markers, filter);
            Debug.Assert(partChanges.Count == markers.Count);
            if (partChanges.Count == 0)
                return (null, null);

            var initSourceText = cat.SourceText.From(templateSource);
            result.sourceText = initSourceText.WithChanges(partChanges);
            result.partRanges = result.sourceText.GetChangeRanges(initSourceText);

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

        public static BuildFilterResult TestFilter(Filter filter) {
            var result = new BuildFilterResult();

            var (sourceText, partRanges) = BuildFilterSource(filter);
            if (sourceText == null) {
                result.NoFilter = true;
            }
            else {
                var diagnostics = RealTimeTraceSession.TestFilter(sourceText);
                result.AddDiagnostics(diagnostics);
                result.AddSourceLines(sourceText.Lines);
                var partLineSpans = GetPartLineSpans(sourceText, partRanges!);
                result.AddPartLineSpans(partLineSpans);
            }

            return result;
        }

        public static bool FilterMethodExists(Filter? filter) {
            if (filter == null)
                return false;
            return filter.FilterParts.Any(fp => string.Equals(fp.Name, "method", StringComparison.OrdinalIgnoreCase) && fp.Lines.Count > 0);
        }

        public BuildFilterResult ApplyProcessingOptions(ProcessingOptions options) {
            var ses = _session;
            if (ses == null)
                throw new InvalidOperationException("No trace session active.");
            var result = new BuildFilterResult();

            if (FilterMethodExists(options.Filter)) {
                var (sourceText, partRanges) = BuildFilterSource(options.Filter);
                if (sourceText == null) {
                    options.Filter = SessionConfig.NoFilter;
                    result.NoFilter = true;
                }
                else {
                    var diagnostics = ses.SetFilter(sourceText, _config);
                    if (diagnostics.Length > 0) {
                        options.Filter = SessionConfig.NoFilter;
                    }
                    result.AddDiagnostics(diagnostics);
                    result.AddSourceLines(sourceText.Lines);
                    var partLineSpans = GetPartLineSpans(sourceText, partRanges!);
                    result.AddPartLineSpans(partLineSpans);
                }
            }
            else {
                // clear filter
                ses.SetFilter(null, _config);
                options.Filter = SessionConfig.NoFilter;
            }

            var oldBatchSize = _processor?.ChangeBatchSize(options.BatchSize) ?? -1;
            var oldMaxWriteDelay = _processor?.ChangeWriteDelay(options.MaxWriteDelayMSecs) ?? -1;

            _sessionConfig.SaveProcessingOptions(options);
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

                if (_sessionConfig.Options.ProcessingOptions.Filter != null) {
                    var (sourceText, partRanges) = BuildFilterSource(_sessionConfig.Options.ProcessingOptions.Filter);
                    if (sourceText != null) {
                        var diagnostics = session.SetFilter(sourceText, _config);
                        if (!diagnostics.IsEmpty) {
                            logger.LogError($"Filter compilation failed.\n{diagnostics}");
                        }
                    }
                }

                // enable the providers
                foreach (var setting in _sessionConfig.Options.ProviderSettings) {
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
                        _sessionConfig.Options.ProcessingOptions.BatchSize,
                        _sessionConfig.Options.ProcessingOptions.MaxWriteDelayMSecs)
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
