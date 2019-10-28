﻿using System;
using System.Collections.Immutable;
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
    class TraceSession: TimedLifeCycleAware
    {
        object _syncObj = new object();

        TraceEventSession _instance;
        public TraceEventSession Instance => CheckDisposed();

        Lazy<RealTimeTraceEventSource> _realTimeSource;

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
                catch (FileNotFoundException) {
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
            if (inst != null) {
                this._instance = null;
                try { inst.Dispose(); }
                catch { }
            }

            var rts = this._realTimeSource;
            if (rts != null) {
                this._realTimeSource = null;
                if (rts.IsValueCreated) {
                    try { rts.Value.Source.Dispose(); }
                    catch { }
                }
            }

            SetFilter(null, null);
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

        void SetFilter(IEventFilter newFilter, CollectibleAssemblyLoadContext newFilterContext) {
            lock (_syncObj) {
                filter = null;
                filterContext?.Unload();

                if (newFilter != null) {
                    filter = newFilter;
                    filterContext = newFilterContext;
                }

                filterChanged = true;
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

        public ImmutableArray<Diagnostic> SetFilter(string filterBody) {
            if (string.IsNullOrWhiteSpace(filterBody)) {
                SetFilter(null, null);
            }

            var compilation = CompileFilter(filterBody);

            Assembly filterAssembly;
            var newFilterContext = new CollectibleAssemblyLoadContext();
            using (var ms = new MemoryStream()) {
                var emitResult = compilation.Emit(ms);
                if (emitResult.Success) {
                    ms.Seek(0, SeekOrigin.Begin);
                    filterAssembly = newFilterContext.LoadFromStream(ms);
                }
                else {
                    return emitResult.Diagnostics;
                }
            }

            var filterType = typeof(IEventFilter);
            var filterClass = filterAssembly.ExportedTypes.Where(tp => tp.IsClass && filterType.IsAssignableFrom(tp)).First();
            var newFilter = (IEventFilter)Activator.CreateInstance(filterClass);

            SetFilter(newFilter, newFilterContext);

            return ImmutableArray<Diagnostic>.Empty;
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

        RealTimeTraceEventSource CreateRealTimeSource(
           Func<tracing.TraceEvent, Task> postEvent,
           TaskCompletionSource<object> tcs,
           CancellationToken cancelToken
        ) {
            return new RealTimeTraceEventSource(this, postEvent, tcs, cancelToken);
        }

        public RealTimeTraceEventSource StartEvents(Func<tracing.TraceEvent, Task> postEvent, TaskCompletionSource<object> tcs, CancellationToken cancelToken) {
            var newRtsLazy = new Lazy<RealTimeTraceEventSource>(() => CreateRealTimeSource(postEvent, tcs, cancelToken), false);
            var oldRtsLazy = Interlocked.Exchange(ref _realTimeSource, newRtsLazy);

            if (oldRtsLazy?.IsValueCreated ?? false) {
                oldRtsLazy.Value.Dispose();
            }

            return newRtsLazy.Value;
        }

        public void StopEvents() {
            Instance.Flush();
            var rtsLazy = Interlocked.Exchange(ref _realTimeSource, null);
            if (rtsLazy?.IsValueCreated ?? false) {
                rtsLazy.Value.Stop();
            }
        }
    }


    public static class TplActivities
    {
        public static readonly Guid TplEventSourceGuid = new Guid("2e5dba47-a3d2-4d16-8ee0-6671ffdcd7b5");
        public static readonly ulong TaskFlowActivityIdsKeyword = 0x80;
    }
}