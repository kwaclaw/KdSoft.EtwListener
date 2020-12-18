using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace KdSoft.EtwEvents.Client.Shared
{
    public static class Utils
    {
        public static IEnumerable<Type> GetLoadableTypes(this Assembly assembly) {
            if (assembly == null)
                throw new ArgumentNullException("assembly");
            try {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e) {
                return e.Types.Where(t => t != null);
            }
        }

        // both types must have been loaded in the same load context
        static string? GetEventSinkType(this Type type, Type? sinkAttributeType) {
            var atts = CustomAttributeData.GetCustomAttributes(type);
            for (int indx = 0; indx < atts.Count; indx++) {
                var att = atts[indx];
                if (att.AttributeType == sinkAttributeType)
                    return att.ConstructorArguments[0].Value as string;
            }
            return null;
        }

        static bool IsEventSinkType(this Type type, Type? sinkAttributeType) {
            var atts = CustomAttributeData.GetCustomAttributes(type);
            for (int indx = 0; indx < atts.Count; indx++) {
                var att = atts[indx];
                if (att.AttributeType == sinkAttributeType)
                    return true;
            }
            return false;
        }

        static IEnumerable<Type> GetEventSinkFactoryTypes(this MetadataLoadContext loadContext, string assemblyPath, out Assembly? factorySharedAssembly) {
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

        public static IEnumerable<string> GetEventSinkTypes(this MetadataLoadContext loadContext, string assemblyPath) {
            var factoryTypes = GetEventSinkFactoryTypes(loadContext, assemblyPath, out var factorySharedAssembly);
            var sinkAttributeType = factorySharedAssembly?.GetType(typeof(EventSinkAttribute).FullName ?? "");
#nullable disable
            return factoryTypes
                .Select(ft => GetEventSinkType(ft, sinkAttributeType))
                .Where(est => est != null);
#nullable enable
        }
    }
}
