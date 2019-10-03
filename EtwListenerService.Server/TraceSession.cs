using System;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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
        object syncObj = new object();

        TraceEventSession instance;
        public TraceEventSession Instance => CheckDisposed();

        TraceEventSession CheckDisposed() {
            var inst = this.instance;
            if (inst == null)
                throw new ObjectDisposedException(nameof(TraceSession));
            return inst;
        }

        public TraceSession(string name, TimeSpan lifeSpan, bool tryAttach = false) : base(lifeSpan) {
            IsCreated = true;
            if (tryAttach) {
                try {
                    instance = new TraceEventSession(name, TraceEventSessionOptions.Attach | TraceEventSessionOptions.NoRestartOnCreate);
                    IsCreated = false;
                }
                catch (FileNotFoundException ex) {
                    instance = new TraceEventSession(name, TraceEventSessionOptions.Create);
                    instance.EnableProvider(TplActivities.TplEventSourceGuid, tracing.TraceEventLevel.Always, TplActivities.TaskFlowActivityIdsKeyword);
                }
            }
            else {
                instance = new TraceEventSession(name, TraceEventSessionOptions.Create);
                instance.EnableProvider(TplActivities.TplEventSourceGuid, tracing.TraceEventLevel.Always, TplActivities.TaskFlowActivityIdsKeyword);
            }
            instance.EnableProviderTimeoutMSec = 10000;
        }

        public bool IsCreated { get; private set; }

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
            lock (syncObj) {
                return filter;
            }
        }

        bool filterChanged;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CheckFilterChanged(ref IEventFilter currentFilter) {
            bool result = filterChanged;
            if (result) {
                filterChanged = false;
                lock (syncObj) {
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

            lock (syncObj) {
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


        protected override void Close() {
            var inst = this.instance;
            if (inst == null)
                return;
            this.instance = null;
            try { inst.Dispose(); }
            catch { }
        }
    }

    public static class TplActivities
    {
        public static readonly Guid TplEventSourceGuid = new Guid("2e5dba47-a3d2-4d16-8ee0-6671ffdcd7b5");
        public static readonly ulong TaskFlowActivityIdsKeyword = 0x80;
    }
}
