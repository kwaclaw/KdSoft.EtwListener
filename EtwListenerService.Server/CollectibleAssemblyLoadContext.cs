using System.Reflection;
using System.Runtime.Loader;

namespace EtwEvents.Server
{
    public class CollectibleAssemblyLoadContext: AssemblyLoadContext
    {
        public CollectibleAssemblyLoadContext() : base(isCollectible: true) { }

        protected override Assembly Load(AssemblyName assemblyName) {
            return null;
        }
    }
}
