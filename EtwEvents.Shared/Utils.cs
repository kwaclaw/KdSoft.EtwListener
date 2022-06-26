using System.Reflection;

namespace KdSoft.EtwEvents
{
    public static class Utils
    {
        public static Assembly? DirectoryResolveAssembly(string assemblyDir, ResolveEventArgs args) {
            var requestedAssembly = new AssemblyName(args.Name);

            var alreadyLoadedAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(ass => {
                    var assName = ass.GetName();
                    return assName.Name == requestedAssembly.Name && assName.Version >= requestedAssembly.Version;
                });

            if (alreadyLoadedAssembly != null) {
                return alreadyLoadedAssembly;
            }

            try {
                var requestedFile = Path.Combine(assemblyDir ?? "", requestedAssembly.Name + ".dll");
                return Assembly.LoadFrom(requestedFile);
            }
            catch (FileNotFoundException) {
                return null;
            }
            catch (FileLoadException) {
                return null;
            }
        }

        public static IEnumerable<Type> GetLoadableTypes(this Assembly assembly) {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));
            try {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e) {
                return (IEnumerable<Type>)e.Types.Where(t => t != null);
            }
        }

        // both types must have been loaded in the same load context
        public static string? GetEventSinkType(this Type type, Type? sinkAttributeType) {
            var atts = CustomAttributeData.GetCustomAttributes(type);
            for (int indx = 0; indx < atts.Count; indx++) {
                var att = atts[indx];
                if (att.AttributeType == sinkAttributeType)
                    return att.ConstructorArguments[0].Value as string;
            }
            return null;
        }

        public static string? GetEventSinkType(this Type type, Assembly? factoryAssembly) {
            var sinkAttributeType = factoryAssembly?.GetType(typeof(EventSinkAttribute).FullName ?? "");
            var atts = CustomAttributeData.GetCustomAttributes(type);
            for (int indx = 0; indx < atts.Count; indx++) {
                var att = atts[indx];
                if (att.AttributeType == sinkAttributeType)
                    return att.ConstructorArguments[0].Value as string;
            }
            return null;
        }

        public static bool IsEventSinkType(this Type type, Type? sinkAttributeType) {
            var atts = CustomAttributeData.GetCustomAttributes(type);
            for (int indx = 0; indx < atts.Count; indx++) {
                var att = atts[indx];
                if (att.AttributeType == sinkAttributeType)
                    return true;
            }
            return false;
        }

        public static bool IsEventSinkType(this Type type, Assembly? factoryAssembly) {
            var sinkAttributeType = factoryAssembly?.GetType(typeof(EventSinkAttribute).FullName ?? "");
            var atts = CustomAttributeData.GetCustomAttributes(type);
            for (int indx = 0; indx < atts.Count; indx++) {
                var att = atts[indx];
                if (att.AttributeType == sinkAttributeType)
                    return true;
            }
            return false;
        }

        public static IEnumerable<Type> GetEventSinkFactoryTypes(this MetadataLoadContext loadContext, string assemblyPath, out Assembly? factorySharedAssembly) {
            factorySharedAssembly = loadContext.LoadFromAssemblyPath(typeof(IEventSinkFactory).Assembly.Location);
            var factoryInterfaceType = factorySharedAssembly?.GetType(typeof(IEventSinkFactory).FullName ?? "");
            var factoryAssembly = loadContext.LoadFromAssemblyPath(assemblyPath);
            var factoryTypes = GetLoadableTypes(factoryAssembly).Where(x => {
                var isFactory = factoryInterfaceType?.IsAssignableFrom(x) ?? false;
                return isFactory && x.IsClass && !x.IsAbstract;
            });
            return factoryTypes;
        }

        public static IEnumerable<Type> GetEventSinkFactoriesBySinkType(this MetadataLoadContext loadContext, string assemblyPath, string sinkType) {
            var factoryTypes = GetEventSinkFactoryTypes(loadContext, assemblyPath, out var factorySharedAssembly);
            var sinkAttributeType = factorySharedAssembly?.GetType(typeof(EventSinkAttribute).FullName ?? "");
            return factoryTypes.Where(f => GetEventSinkType(f, sinkAttributeType) == sinkType);
        }

        public static IEnumerable<Type> GetEventSinkFactories(this MetadataLoadContext loadContext, string assemblyPath) {
            var factoryTypes = GetEventSinkFactoryTypes(loadContext, assemblyPath, out var factorySharedAssembly);
            var sinkAttributeType = factorySharedAssembly?.GetType(typeof(EventSinkAttribute).FullName ?? "");
            return factoryTypes.Where(ft => IsEventSinkType(ft, sinkAttributeType));
        }

        public static IEnumerable<(string sinkType, string? version)> GetEventSinkTypes(this MetadataLoadContext loadContext, string assemblyPath) {
            var factoryTypes = GetEventSinkFactoryTypes(loadContext, assemblyPath, out var factorySharedAssembly);
            var sinkAttributeType = factorySharedAssembly?.GetType(typeof(EventSinkAttribute).FullName ?? "");
#nullable disable
            return factoryTypes
                .Select(ft => (sinkType: GetEventSinkType(ft, sinkAttributeType), version: ft.Assembly.GetName().Version?.ToString()))
                .Where(est => est.sinkType != null);
#nullable enable
        }
    }
}
