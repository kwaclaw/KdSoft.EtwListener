using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Loader;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.Collections;
using KdSoft.EtwEvents.Client.Shared;
using KdSoft.EtwEvents.Server;
using KdSoft.EtwLogging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KdSoft.EtwEvents.PushAgent
{
    class SessionWorker: BackgroundService
    {
        readonly HostBuilderContext _context;
        readonly IOptions<EventQueueOptions> _eventQueueOptions;
        readonly EventSinkService _sinkService;
        readonly EventSinkHolder _sinkHolder;
        readonly ILoggerFactory _loggerFactory;
        readonly ILogger<SessionWorker> _logger;
        readonly JsonSerializerOptions _jsonOptions;

        RealTimeTraceSession? _session;
        public RealTimeTraceSession? Session => _session;

        EventSinkProfile? _sinkProfile;
        public EventSinkProfile? EventSinkProfile => _sinkProfile;
        public Exception? EventSinkError => _sinkHolder.FailedEventSinks.FirstOrDefault().Value.error;

        public SessionWorker(
            HostBuilderContext context,
            HttpClient http,
            IOptions<EventQueueOptions> eventQueueOptions,
            EventSinkService sinkService,
            ILoggerFactory loggerFactory
        ) {
            this._context = context;
            this._eventQueueOptions = eventQueueOptions;
            this._sinkService = sinkService;
            this._loggerFactory = loggerFactory;
            this._logger = loggerFactory.CreateLogger<SessionWorker>();

            _sinkHolder = new EventSinkHolder();
            _jsonOptions = new JsonSerializerOptions {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                AllowTrailingCommas = true,
                WriteIndented = true
            };
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

        bool LoadSinkProfile(out EventSinkProfile profile) {
            try {
                var sinkOptionsJson = File.ReadAllText(EventSinkOptionsPath);
                profile = JsonSerializer.Deserialize<EventSinkProfile>(sinkOptionsJson, _jsonOptions) ?? new EventSinkProfile();
                return true;
            }
            catch (Exception ex) {
                profile = new EventSinkProfile();
                _logger.LogError(ex, "Error loading event sink options.");
                return false;
            }
        }

        bool SaveSinkProfile(EventSinkProfile profile) {
            try {
                var json = JsonSerializer.Serialize(profile, _jsonOptions);
                File.WriteAllText(EventSinkOptionsPath, json);
                return true;
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error saving event sink options.");
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
            SaveProviderSettings(providerSettings);
        }

        public BuildFilterResult ApplyFilter(string filter) {
            var ses = _session;
            if (ses == null)
                throw new InvalidOperationException("No trace session active.");
            var diagnostics = ses.SetFilter(filter);
            var filterResult = new BuildFilterResult().AddDiagnostics(diagnostics);
            if (diagnostics.Length == 0) {
                SaveFilterSettings(filter);
            }
            return filterResult;
        }

        public BuildFilterResult TestFilter(string filter) {
            var diagnostics = RealTimeTraceSession.TestFilter(filter);
            return new BuildFilterResult().AddDiagnostics(diagnostics);
        }

        async Task<IEventSinkFactory?> LoadSinkFactory(string sinkType, string version) {
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

        // this can be called from two locations: ConfigureEventSinkClosure() and ExecuteAsync()->CloseEventSinks()
        void UnloadEventSink(string name, IEventSink eventSink) {
            // unload only once! otherwise we get a System.ExecutionEngineException
            if (_sinkHolder.DeleteEventSink(name)) {
                var sinkAssembly = eventSink.GetType().Assembly;
                if (sinkAssembly != null) {
                    var loadContext = AssemblyLoadContext.GetLoadContext(sinkAssembly);
                    (loadContext as CollectibleAssemblyLoadContext)?.Unload();
                }
            }
        }

        async Task CloseEventSink(string name, IEventSink sink, bool alreadyDisposed) {
            try {
                if (!alreadyDisposed) { // was not disposed
                    await sink.DisposeAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex) {
                _logger.LogError(ex, $"Error closing event sink '{name}'.");
            }
            finally {
                UnloadEventSink(name, sink);
            }
        }

        Task ConfigureEventSinkClosure(string name, IEventSink sink) {
            return sink.RunTask.ContinueWith(rt => CloseEventSink(name, sink, !rt.Result), TaskScheduler.Default);
        }

        async Task CloseEventSinks() {
            var (activeSinks, failedSinks) = _sinkHolder.ClearEventSinks();
            var disposeEntries = activeSinks.Select(sink => (sink.Key, CloseEventSink(sink.Key, sink.Value, false))).ToArray();
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
                SaveSinkProfile(sinkProfile);
                _sinkProfile = sinkProfile;
            }
            catch (Exception ex) {
                await sink.DisposeAsync().ConfigureAwait(false);
                _logger.LogError(ex, $"Error updating event sink '{sinkProfile.Name}'.");
                throw;
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            try {
                var sessionOptionsAvailable = LoadSessionOptions(out var sessionOptions);
                if (!sessionOptionsAvailable) {
                    _logger.LogInformation("Starting session without configured options.");
                }

                var sinkProfileAvailable = LoadSinkProfile(out var sinkOptions);

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

                if (sinkProfileAvailable) {
                    await UpdateEventSink(sinkOptions).ConfigureAwait(false);
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
                    using (var processor = new PersistentEventProcessor(writeBatch, _eventQueueOptions.Value.FilePath, processorLogger, sessionOptions.BatchSize)) {
                        var maxWriteDelay = TimeSpan.FromMilliseconds(sessionOptions.MaxWriteDelayMSecs);
                        _logger.LogInformation("SessionWorker started.");
                        await processor.Process(session, maxWriteDelay, stoppingToken).ConfigureAwait(false);
                    }

                }
                finally {
                    await CloseEventSinks().ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) {
                _logger.LogInformation("SessionWorker stopped.");
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Failure running service.");
            }
        }

        public override void Dispose() {
            base.Dispose();
            _session?.Dispose();
        }
    }
}
