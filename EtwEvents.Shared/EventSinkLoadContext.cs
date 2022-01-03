using System.Reflection;
using System.Runtime.Loader;

namespace KdSoft.EtwEvents
{

    /// <summary>
    /// Collectible AssemblyLoadContext for event sinks.
    /// </summary>
    /// <remarks>
    /// One can initiate unloading of the EventSinkLoadContext by either calling its Unload method
    /// getting rid of the reference to the AssemblyLoadContext, e.g. by just using a local variable.
    /// See https://docs.microsoft.com/en-us/dotnet/standard/assembly/unloadability
    /// and https://docs.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support
    /// </remarks>
    public class EventSinkLoadContext: AssemblyLoadContext
    {
        readonly AssemblyDependencyResolver _resolver;
        public EventSinkLoadContext(string eventSinkPath) : base(isCollectible: true) {
            this._resolver = new AssemblyDependencyResolver(eventSinkPath);
        }

        protected override Assembly? Load(AssemblyName assemblyName) {
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
