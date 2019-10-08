using System;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using KdSoft.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Diagnostics.Tracing.Session;
using tracing = Microsoft.Diagnostics.Tracing;

namespace EtwEvents.Server
{
    public class TraceSession: TimedLifeCycleAware
    {
        object _syncObj = new object();

        TraceEventSession _instance;
        public TraceEventSession Instance => CheckDisposed();

        TraceEventSession CheckDisposed() {
            var inst = this._instance;
            if (inst == null)
                throw new ObjectDisposedException(nameof(TraceSession));
            return inst;
        }

        public TraceSession(string name, TimeSpan lifeSpan, bool tryAttach = false) : base(lifeSpan) {
            IsCreated = true;
            if (tryAttach) {
                try {
                    _instance = new TraceEventSession(name, TraceEventSessionOptions.Attach | TraceEventSessionOptions.NoRestartOnCreate);
                    IsCreated = false;
                }
                catch (FileNotFoundException ex) {
                    _instance = new TraceEventSession(name, TraceEventSessionOptions.Create);
                    _instance.EnableProvider(TplActivities.TplEventSourceGuid, tracing.TraceEventLevel.Always, TplActivities.TaskFlowActivityIdsKeyword);
                }
            }
            else {
                _instance = new TraceEventSession(name, TraceEventSessionOptions.Create);
                _instance.EnableProvider(TplActivities.TplEventSourceGuid, tracing.TraceEventLevel.Always, TplActivities.TaskFlowActivityIdsKeyword);
            }
            _instance.EnableProviderTimeoutMSec = 10000;
        }

        public bool IsCreated { get; private set; }

        protected override void Close() {
            var inst = this._instance;
            if (inst == null)
                return;
            this._instance = null;
            try { inst.Dispose(); }
            catch { }
        }

        #region Filters

        const string filterTemplate = @"
using System;
using Microsoft.Diagnostics.Tracing;

namespace EtwEvents.Server
{{
    public class EventFilter: IEventFilter
    {{
        public bool IncludeEvent(TraceEvent evt) {{
            {0}
        }}
    }}
}}
";
        static Assembly SystemRuntime = Assembly.Load(new AssemblyName("System.Runtime"));
        static Assembly NetStandard20 = Assembly.Load("netstandard, Version=2.0.0.0");

        CollectibleAssemblyLoadContext filterContext;
        IEventFilter filter;
        public IEventFilter GetFilter() {
            lock (_syncObj) {
                return filter;
            }
        }

        bool filterChanged;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CheckFilterChanged(ref IEventFilter currentFilter) {
            bool result = filterChanged;
            if (result) {
                filterChanged = false;
                lock (_syncObj) {
                    currentFilter = this.filter;
                }
            }
            return result;
        }

        public void SetFilter(string filterBody) {
            var compilation = CompileFilter(filterBody);

            Assembly filterAssembly;
            var newFilterContext = new CollectibleAssemblyLoadContext();
            using (var ms = new MemoryStream()) {
                var cr = compilation.Emit(ms);
                ms.Seek(0, SeekOrigin.Begin);
                filterAssembly = newFilterContext.LoadFromStream(ms);
            }

            var filterType = typeof(IEventFilter);
            var filterClass = filterAssembly.ExportedTypes.Where(tp => tp.IsClass && filterType.IsAssignableFrom(tp)).First();
            var newFilter = (IEventFilter)Activator.CreateInstance(filterClass);

            lock (_syncObj) {
                filter = null;
                filterContext?.Unload();

                filter = newFilter;
                filterContext = newFilterContext;
                filterChanged = true;
            }
        }

        public static CSharpCompilation CompileFilter(string filterBody) {
            var sourceCode = string.Format(filterTemplate, filterBody);
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

        Action<tracing.TraceEvent> handleEvent = null;
        Action handleCompleted = null;

        int started = 0;
        int stopped = 0;
        public bool StartEvents(Func<tracing.TraceEvent, Task> postEvent, TaskCompletionSource<object> tcs, CancellationToken cancelToken) {
            var filter = GetFilter(); // this performs a lock

            Action<tracing.TraceEvent> newHandleEvent = async (tracing.TraceEvent evt) => {
                if (this.stopped != 0) {
                    var inst = this._instance;
                    inst?.Source.StopProcessing();
                    tcs.TrySetResult(null);
                    return;
                }
                if (cancelToken.IsCancellationRequested) {
                    // instance.Source.StopProcessing(); cannot continue once we have stopped
                    return;
                }
                if (TplActivities.TplEventSourceGuid.Equals(evt.ProviderGuid))
                    return;

                CheckFilterChanged(ref filter);
                if (filter == null || filter.IncludeEvent(evt)) {
                    await postEvent(evt).ConfigureAwait(false);
                }
            };
            var oldHandleEvent = Interlocked.Exchange(ref handleEvent, newHandleEvent);
            if (oldHandleEvent != null) 
                Instance.Source.Dynamic.All -= oldHandleEvent;
            Instance.Source.Dynamic.All += newHandleEvent;

            Action newHandleCompleted = () => tcs.TrySetResult(null);
            var oldHandleCompleted = Interlocked.Exchange(ref handleCompleted, newHandleCompleted);
            if (oldHandleCompleted != null) 
                Instance.Source.Completed -= oldHandleCompleted;
            Instance.Source.Completed += newHandleCompleted;

            var oldStarted = Interlocked.CompareExchange(ref started, 1, 0);
            if (oldStarted == 1 && !Instance.Source.CanReset) { // real time sessions cannot be reset
                return false;
            }
            // this cannot be called multiple times in real time mode
            Instance.Source.Process();
            return true;
        }

        public void StopEvents() {
            Instance.Flush();
            this.stopped = -1;
        }
    }

    public static class TplActivities
    {
        public static readonly Guid TplEventSourceGuid = new Guid("2e5dba47-a3d2-4d16-8ee0-6671ffdcd7b5");
        public static readonly ulong TaskFlowActivityIdsKeyword = 0x80;
    }
}
