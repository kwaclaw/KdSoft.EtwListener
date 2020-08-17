using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

        static string? GetEventSinkType(Type type) {
            var atts = CustomAttributeData.GetCustomAttributes(type);
            for (int indx = 0; indx < atts.Count; indx++) {
                var att = atts[indx];
                if (att.AttributeType == typeof(EventSinkAttribute))
                    return att.ConstructorArguments[0].Value as string;
            }
            return null;
        }

        public static IEnumerable<Type> GetEventSinkFactoriesBySinkType(this Assembly assembly, string sinkType) {
            var factoryType = typeof(IEventSinkFactory);
            var factories = GetLoadableTypes(assembly).Where(x => factoryType.IsAssignableFrom(x) && x.IsClass && !x.IsAbstract);
            return factories.Where(f => GetEventSinkType(f) == sinkType);
        }

        public static IEnumerable<(Type factoryType, string sinkType)> GetEventSinkFactories(this Assembly assembly) {
            var factoryType = typeof(IEventSinkFactory);
            var factoryTypes = GetLoadableTypes(assembly).Where(x => factoryType.IsAssignableFrom(x) && x.IsClass && !x.IsAbstract);
            return factoryTypes.Select(ft => (ft, GetEventSinkType(ft) ?? "")).Where(valueTuple => !string.IsNullOrEmpty(valueTuple.Item2));
        }
    }
}
