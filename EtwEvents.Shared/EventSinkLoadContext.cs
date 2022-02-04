using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;

namespace KdSoft.EtwEvents
{
    /// <summary>
    /// Collectible AssemblyLoadContext for event sinks.
    /// </summary>
    /// <remarks>
    /// One can initiate unloading of the EventSinkLoadContext by either calling its Unload method,
    /// or by getting rid of the reference to the AssemblyLoadContext, e.g. by just using a local variable.
    /// See https://docs.microsoft.com/en-us/dotnet/standard/assembly/unloadability
    /// and https://docs.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support
    /// </remarks>
    public class EventSinkLoadContext: AssemblyLoadContext
    {
        readonly AssemblyDependencyResolver _resolver;
        readonly HashSet<AssemblyName> _sharedAssemblies;

        public class AssemblyNameEqualityComparer: EqualityComparer<AssemblyName>
        {
            public override bool Equals(AssemblyName? x, AssemblyName? y) => AssemblyName.ReferenceMatchesDefinition(x, y);
            public override int GetHashCode([DisallowNull] AssemblyName obj) => obj.Name?.GetHashCode() ?? 0;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="eventSinkPath">Path to main assembly file, that has an associated *.deps.json file.</param>
        /// <param name="sharedAssemblies">Assembly names that should not be loaded even if they can be resolved to a local assembly.</param>
        public EventSinkLoadContext(string eventSinkPath, params AssemblyName[] sharedAssemblies) : base(isCollectible: true) {
            _resolver = new AssemblyDependencyResolver(eventSinkPath);
            _sharedAssemblies = new HashSet<AssemblyName>(sharedAssemblies, new AssemblyNameEqualityComparer());
        }

        protected override Assembly? Load(AssemblyName assemblyName) {
            // don't load shared assemblies in this load context
            if (_sharedAssemblies.Contains(assemblyName)) {
                return null;
            }

            string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null) {
                return LoadFromAssemblyPath(assemblyPath);
            }

            return null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName) {
            string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (libraryPath != null) {
                return LoadUnmanagedDllFromPath(libraryPath);
            }

            return IntPtr.Zero;
        }
    }
}
