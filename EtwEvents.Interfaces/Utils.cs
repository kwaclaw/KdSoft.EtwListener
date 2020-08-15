using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

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

        static bool HasEventSinkAttribute(Type type, string name) {
            var atts = Attribute.GetCustomAttributes(type, typeof(EventSinkAttribute));
            for (int indx = 0; indx < atts.Length; indx++) {
                var att = (EventSinkAttribute)atts[indx];
                if (string.Equals(att.Name, name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public static IEnumerable<Type> GetEventSinkFactories(this Assembly assembly, string name) {
            var factoryType = typeof(IEventSinkFactory);
            var factories = GetLoadableTypes(assembly).Where(x => factoryType.IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract);
            return factories.Where(f => HasEventSinkAttribute(f, name));
        }
    }
}
