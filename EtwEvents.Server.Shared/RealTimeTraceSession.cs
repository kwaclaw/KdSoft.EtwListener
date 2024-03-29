﻿using System.Collections.Immutable;
using System.Dynamic;
using System.Reflection;
using System.Runtime.CompilerServices;
using KdSoft.EtwLogging;
using KdSoft.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MdTracing = Microsoft.Diagnostics.Tracing;

namespace KdSoft.EtwEvents.Server
{
    public class RealTimeTraceSession: TimedLifeCycleAware, IDisposable
    {
        readonly ILogger<RealTimeTraceSession> _logger;

        TraceEventSession? _instance;
        TraceEventSession Instance => CheckDisposed();

        ImmutableDictionary<string, ProviderSetting> _enabledProviders = ImmutableDictionary<string, ProviderSetting>.Empty;
        public IEnumerable<ProviderSetting> EnabledProviders => _enabledProviders.Values;

        public string SessionName => Instance.SessionName;

        #region Construction

        TraceEventSession CheckDisposed() {
            var inst = _instance ?? throw new ObjectDisposedException(nameof(RealTimeTraceSession));
            return inst;
        }

        public RealTimeTraceSession(string name, TimeSpan lifeSpan, ILogger<RealTimeTraceSession> logger, bool tryAttach = false) : base(lifeSpan) {
            _logger = logger;

            IsCreated = true;
            if (tryAttach) {
                try {
                    _instance = new TraceEventSession(name, TraceEventSessionOptions.Attach | TraceEventSessionOptions.NoRestartOnCreate);
                    IsCreated = false;
                }
                catch (FileNotFoundException) {
                    _instance = new TraceEventSession(name, TraceEventSessionOptions.Create);
                }
            }
            else {
                _instance = new TraceEventSession(name, TraceEventSessionOptions.Create);
            }

            if (IsCreated) {
                _instance.EnableProviderTimeoutMSec = 10000;
                // we don't want the trace session to keep running
                _instance.StopOnDispose = true;
                _instance.EnableProvider(TplActivities.TplEventSourceGuid, MdTracing.TraceEventLevel.Always, TplActivities.TaskFlowActivityIdsKeyword);
            }
        }

        public bool IsCreated { get; private set; }

        // will be called through life cycle management
        protected override void Close() {
            var inst = _instance;
            if (inst != null) {
                _instance = null;
                try {
                    inst.Flush();
                    // when StopOnDispose = false then it appears the trace session will not be shut down on Dispose()!
                    // using "logman query -ets" to check
                    //inst.Stop();
                    inst.Dispose();
                }
                catch { /* ignore */ }
            }

            SetFilterHolder(null);
        }

        // not necessary to call explicitly as life cycle management already does it
        public void Dispose() {
            GC.SuppressFinalize(this);
            Close();
        }

        #endregion

        #region Events

        int _isStarted;
        /// <summary>
        /// Once started, IsStarted will always be true, as a real time session cannot be restarted.
        /// </summary>
        public bool IsStarted {
            get {
                Interlocked.MemoryBarrier();
                var result = _isStarted != 0;
                Interlocked.MemoryBarrier();
                return result;
            }
        }

        int _isStopped;
        /// <summary>
        /// Once stopped, IsStopped will always be true, as a real time session cannot be restarted.
        /// </summary>
        public bool IsStopped {
            get {
                Interlocked.MemoryBarrier();
                var result = _isStopped != 0;
                Interlocked.MemoryBarrier();
                return result;
            }
        }

        public Task<bool> StartEvents(Action<MdTracing.TraceEvent> postEvent, CancellationToken cancelToken) {
            CheckDisposed();

            int alreadyStarted = Interlocked.CompareExchange(ref _isStarted, 1, 0);
            if (alreadyStarted != 0) {
                throw new InvalidOperationException("A real time trace event session cannot be re-started!");
            }

            cancelToken.Register(() => {
                Dispose();
            });

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void handleEvent(TraceEvent evt) {
                try {
                    if (cancelToken.IsCancellationRequested) {
                        return;
                    }
                    if (TplActivities.TplEventSourceGuid.Equals(evt.ProviderGuid))
                        return;

                    var filter = GetCurrentFilter();
                    if (filter == null || filter.IncludeEvent(evt)) {
                        postEvent(evt);
                    }
                }
                catch (Exception ex) {
                    _logger.LogError(ex, "Error in {method}", nameof(handleEvent));
                }
            }
            Instance.Source.Dynamic.All += handleEvent;

            // save locally, as SessionName might throw an ObjectDisposedException in some handlers later
            string sessionName = SessionName;

            void handleCompleted() {
                Interlocked.MemoryBarrier();
                _isStopped = 1;
                Interlocked.MemoryBarrier();
                _logger.LogInformation("{sessionType} '{sessionName}' has finished.", nameof(RealTimeTraceSession), sessionName);
            }
            Instance.Source.Completed += handleCompleted;

            // this cannot be called multiple times for a given real-time session;
            // once stopped we will need to close and re-open the TraceSession to continue
            var processTask = Task.Run<bool>(Instance.Source.Process);
            _logger.LogInformation("{sessionType} '{sessionName}' has started.", nameof(RealTimeTraceSession), sessionName);

            var continuedTask = processTask.ContinueWith<bool>(t => {
                Interlocked.MemoryBarrier();
                _isStopped = 1;
                Interlocked.MemoryBarrier();
                if (t.IsFaulted)
                    _logger.LogError(t.Exception, "Error in {sessionType} '{sessionName}'.", nameof(RealTimeTraceSession), sessionName);
                else if (t.IsCanceled)
                    _logger.LogWarning("Cancelled {sessionType} '{sessionName}'.", nameof(RealTimeTraceSession), sessionName);
                return t.Result;
            }, TaskContinuationOptions.ExecuteSynchronously);

            return continuedTask;
        }

        /// <summary>
        /// Stops trace session from delivering events.
        /// A real time trace session cannot be restarted after that, so this API is not really useful.
        /// </summary>
        public void StopEvents() {
            Instance.Flush();
            Instance.Stop();
            Interlocked.MemoryBarrier();
            _isStopped = 1;
            Interlocked.MemoryBarrier();
        }

        public bool EnableProvider(ProviderSetting setting) {
            var result = Instance.EnableProvider(setting.Name, (MdTracing.TraceEventLevel)setting.Level, setting.MatchKeywords);
            _logger.LogInformation("Enabled provider '{provider}': {enabled}.", setting.Name, result);
            _enabledProviders = _enabledProviders.SetItem(setting.Name, setting);
            return result;
        }

        public void DisableProvider(string provider) {
            Instance.DisableProvider(provider);
            _logger.LogInformation("Disabled provider '{provider}'.", provider);
            _enabledProviders = _enabledProviders.Remove(provider);
        }

        #endregion

        #region Filters

        static readonly Assembly SystemRuntime = Assembly.Load(new AssemblyName("System.Runtime"));
        static readonly Assembly SystemTextRegex = Assembly.Load(new AssemblyName("System.Text.RegularExpressions"));
        static readonly Assembly SystemLinq = Assembly.Load(new AssemblyName("System.Linq"));
        static readonly Assembly NetStandard20 = Assembly.Load("netstandard, Version=2.0.0.0");

        class FilterHolder
        {
            public FilterHolder(IEventFilter filter, CollectibleAssemblyLoadContext loadContext, SourceText? filterSource) {
                this.Filter = filter;
                this.LoadContext = loadContext;
                this.FilterSource = filterSource;
            }
            public readonly IEventFilter Filter;
            public readonly CollectibleAssemblyLoadContext LoadContext;
            public readonly SourceText? FilterSource;
        }

        FilterHolder? _filterHolder;
        void SetFilterHolder(FilterHolder? holder) {
            var oldFilterHolder = Interlocked.Exchange(ref _filterHolder, holder);
            oldFilterHolder?.LoadContext.Unload();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEventFilter? GetCurrentFilter() {
            var filterHolder = _filterHolder;
            return filterHolder?.Filter;
        }

        public SourceText? GetCurrentFilterSource() {
            var filterHolder = _filterHolder;
            return filterHolder?.FilterSource;
        }

        public static ImmutableArray<Diagnostic> GenerateFilter(SourceText filterSource, MemoryStream ms) {
            var compilation = CompileFilter(filterSource);
            var emitResult = compilation.Emit(ms);
            if (emitResult.Success) {
                return ImmutableArray<Diagnostic>.Empty;
            }
            return emitResult.Diagnostics;
        }

        public static ImmutableArray<Diagnostic> TestFilter(SourceText? filterSource) {
            if (filterSource == null) {
                return ImmutableArray<Diagnostic>.Empty;
            }
            using var ms = new MemoryStream();
            return GenerateFilter(filterSource, ms);
        }

        public ImmutableArray<Diagnostic> SetFilter(SourceText? filterSource, IConfiguration config) {
            CheckDisposed();

            if (filterSource == null) {
                SetFilterHolder(null);
                return ImmutableArray<Diagnostic>.Empty;
            }

            ImmutableArray<Diagnostic> result;

            Assembly filterAssembly;
            var newFilterContext = new CollectibleAssemblyLoadContext();
            using (var ms = new MemoryStream()) {
                result = GenerateFilter(filterSource, ms);
                if (result.Length == 0) {
                    ms.Seek(0, SeekOrigin.Begin);
                    filterAssembly = newFilterContext.LoadFromStream(ms);
                }
                else {
                    return result;
                }
            }

            var filterType = typeof(IEventFilter);
            var filterClass = filterAssembly.ExportedTypes.Where(tp => tp.IsClass && filterType.IsAssignableFrom(tp)).First();
            if (filterClass == null) {
                return ImmutableArray<Diagnostic>.Empty.Add(Diagnostic.Create(
                    "FL1001", "Filter", "Cannot find IEventFilter instance.", DiagnosticSeverity.Error, DiagnosticSeverity.Error, true, 0
                ));
            }
            var newFilter = (IEventFilter?)Activator.CreateInstance(filterClass, config);

            SetFilterHolder(new FilterHolder(newFilter!, newFilterContext, filterSource));

            return result;
        }

        public static CSharpCompilation CompileFilter(SourceText filterSource) {
            var options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);

            var parsedSyntaxTree = SyntaxFactory.ParseSyntaxTree(filterSource, options);
            var references = new MetadataReference[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(DynamicObject).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(TraceEventSession).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IEventFilter).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IConfiguration).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(ConfigurationBuilder).Assembly.Location),
                MetadataReference.CreateFromFile(SystemRuntime.Location),
                MetadataReference.CreateFromFile(SystemTextRegex.Location),
                MetadataReference.CreateFromFile(SystemLinq.Location),
                MetadataReference.CreateFromFile(NetStandard20.Location),
            };

            var compilation = CSharpCompilation.Create(
                "FilterAssembly",
                new[] { parsedSyntaxTree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );

            return compilation;
        }

        #endregion
    }

    public static class TplActivities
    {
        public static readonly Guid TplEventSourceGuid = new("2e5dba47-a3d2-4d16-8ee0-6671ffdcd7b5");
        public static readonly ulong TaskFlowActivityIdsKeyword = 0x80;
    }
}
