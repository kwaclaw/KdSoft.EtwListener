using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using KdSoft.EtwLogging;
using KdSoft.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Extensions.Logging;
using tracing = Microsoft.Diagnostics.Tracing;

namespace KdSoft.EtwEvents.Server
{
    class RealTimeTraceSession: TimedLifeCycleAware, IDisposable {
        readonly object _syncObj = new object();
        readonly ILogger<RealTimeTraceSession> _logger;

        TraceEventSession? _instance;
        TraceEventSession Instance => CheckDisposed();

        ImmutableDictionary<string, ProviderSetting> _enabledProviders = ImmutableDictionary<string, ProviderSetting>.Empty;
        public IEnumerable<ProviderSetting> EnabledProviders => _enabledProviders.Values;

        public string SessionName => Instance.SessionName;

        //public ETWTraceEventSource Source => Instance.Source;

        #region Construction

        TraceEventSession CheckDisposed() {
            var inst = _instance;
            if (inst == null)
                throw new ObjectDisposedException(nameof(RealTimeTraceSession));
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
                _instance.StopOnDispose = false;
                _instance.EnableProvider(TplActivities.TplEventSourceGuid, tracing.TraceEventLevel.Always, TplActivities.TaskFlowActivityIdsKeyword);
            }
        }

        public bool IsCreated { get; private set; }

        // will be called through life cycle management
        protected override void Close() {
            var inst = _instance;
            if (inst != null) {
                _instance = null;
                try { inst.Dispose(); }
                catch { /* ignore */ }
            }

            SetFilter(null, null);
        }

        // not necessary to call explicitly as life cycle management already does it
        public void Dispose() {
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

        public Task<bool> StartEvents(Func<tracing.TraceEvent, Task> postEvent, CancellationToken cancelToken) {
            CheckDisposed();

            int alreadyStarted = Interlocked.CompareExchange(ref _isStarted, 1, 0);
            if (alreadyStarted != 0) {
                throw new InvalidOperationException("A real time trace event session cannot be re-started!");
            }

            var filter = GetFilter();
            async void handleEvent(TraceEvent evt) {
                try {
                    if (cancelToken.IsCancellationRequested) {
                        Instance.Source.Dispose();
                        return;
                    }
                    if (TplActivities.TplEventSourceGuid.Equals(evt.ProviderGuid))
                        return;

                    CheckFilterChanged(ref filter);
                    if (filter == null || filter.IncludeEvent(evt)) {
                        await postEvent(evt).ConfigureAwait(false);
                    }
                }
                catch (Exception ex) {
                    _logger.LogError(ex, $"Error in {nameof(handleEvent)}");
                }

            }
            Instance.Source.Dynamic.All += handleEvent;

            void handleCompleted() {
                _logger.LogInformation($"{nameof(RealTimeTraceSession)} '{SessionName}' has finished.");
            }
            Instance.Source.Completed += handleCompleted;

            // this cannot be called multiple times for a given real-time session;
            // once stopped we will need to close and re-open the TraceSession to continue
            var processTask = Task.Run<bool>(Instance.Source.Process);
            _logger.LogInformation($"{nameof(RealTimeTraceSession)} '{SessionName}' has started.");

            processTask.ContinueWith(t => {
                _logger.LogError(t.Exception, $"Error in {nameof(RealTimeTraceSession)} '{SessionName}'.");
            }, TaskContinuationOptions.OnlyOnFaulted);

            return processTask;
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
            var result = Instance.EnableProvider(setting.Name, (tracing.TraceEventLevel)setting.Level, setting.MatchKeywords);
            _enabledProviders = _enabledProviders.SetItem(setting.Name, setting);
            return result;
        }

        public void DisableProvider(string provider) {
            Instance.DisableProvider(provider);
            _enabledProviders = _enabledProviders.Remove(provider);
        }

        #endregion

        #region Filters

        const string FilterTemplate = @"
using System;
using Microsoft.Diagnostics.Tracing;

namespace KdSoft.EtwEvents.Server
{{
    public class EventFilter: IEventFilter
    {{
        public bool IncludeEvent(TraceEvent evt) {{
            {0}
        }}
    }}
}}
";
        static readonly Assembly SystemRuntime = Assembly.Load(new AssemblyName("System.Runtime"));
        static readonly Assembly NetStandard20 = Assembly.Load("netstandard, Version=2.0.0.0");

        CollectibleAssemblyLoadContext? filterContext;
        IEventFilter? filter;
        public IEventFilter? GetFilter() {
            lock (_syncObj) {
                return filter;
            }
        }

        void SetFilter(IEventFilter? newFilter, CollectibleAssemblyLoadContext? newFilterContext) {
            lock (_syncObj) {
                filter = null;
                filterContext?.Unload();

                if (newFilter != null) {
                    filter = newFilter;
                    filterContext = newFilterContext;
                }

                Interlocked.Exchange(ref filterChanged, 1);
            }
        }

        int filterChanged;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CheckFilterChanged(ref IEventFilter? currentFilter) {
            var currentFilterChanged = Interlocked.CompareExchange(ref filterChanged, 0, 1);
            if (currentFilterChanged == 1) {
                lock (_syncObj) {
                    currentFilter = filter;
                }
                return true;
            }
            return false;
        }

        public static ImmutableArray<Diagnostic> GenerateFilter(string filterBody, MemoryStream ms) {
            var compilation = CompileFilter(filterBody);
            var emitResult = compilation.Emit(ms);
            if (emitResult.Success) {
                return ImmutableArray<Diagnostic>.Empty;
            }
            return emitResult.Diagnostics;
        }

        public static ImmutableArray<Diagnostic> TestFilter(string filterBody) {
            if (string.IsNullOrWhiteSpace(filterBody)) {
                return ImmutableArray<Diagnostic>.Empty;
            }
            using (var ms = new MemoryStream()) {
                var diagnostics = GenerateFilter(filterBody, ms);
                if (diagnostics.Length > 0) {
                    return diagnostics;
                }
            }
            return ImmutableArray<Diagnostic>.Empty;
        }

        public ImmutableArray<Diagnostic> SetFilter(string filterBody) {
            CheckDisposed();

            if (string.IsNullOrWhiteSpace(filterBody)) {
                SetFilter(null, null);
            }

            Assembly filterAssembly;
            var newFilterContext = new CollectibleAssemblyLoadContext();
            using (var ms = new MemoryStream()) {
                var diagnostics = GenerateFilter(filterBody, ms);
                if (diagnostics.Length == 0) {
                    ms.Seek(0, SeekOrigin.Begin);
                    filterAssembly = newFilterContext.LoadFromStream(ms);
                }
                else {
                    return diagnostics;
                }
            }

            var filterType = typeof(IEventFilter);
            var filterClass = filterAssembly.ExportedTypes.Where(tp => tp.IsClass && filterType.IsAssignableFrom(tp)).First();
            var newFilter = (IEventFilter?)Activator.CreateInstance(filterClass);

            SetFilter(newFilter, newFilterContext);

            return ImmutableArray<Diagnostic>.Empty;
        }

        public static CSharpCompilation CompileFilter(string filterBody) {
            var sourceCode = string.Format(CultureInfo.InvariantCulture,  FilterTemplate, filterBody);
            var sourceText = SourceText.From(sourceCode);
            var options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);

            var parsedSyntaxTree = SyntaxFactory.ParseSyntaxTree(sourceText, options);
            var references = new MetadataReference[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(DynamicObject).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(TraceEventSession).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IEventFilter).Assembly.Location),
                MetadataReference.CreateFromFile(SystemRuntime.Location),
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
        public static readonly Guid TplEventSourceGuid = new Guid("2e5dba47-a3d2-4d16-8ee0-6671ffdcd7b5");
        public static readonly ulong TaskFlowActivityIdsKeyword = 0x80;
    }
}
