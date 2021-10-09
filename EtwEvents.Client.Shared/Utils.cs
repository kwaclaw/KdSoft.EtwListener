using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;

namespace KdSoft.EtwEvents.Client.Shared
{
    public static class Utils
    {
        public static Assembly? DirectoryResolveAssembly(string assemblyDir, ResolveEventArgs args) {
            var requestedAssembly = new AssemblyName(args.Name);

            var alreadyLoadedAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == requestedAssembly.Name);

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

        public static IEnumerable<Type> GetEventSinkFactoriesBySinkType(this MetadataLoadContext loadContext, string assemblyPath, string sinkType, out Assembly? factorySharedAssembly) {
            var factoryTypes = GetEventSinkFactoryTypes(loadContext, assemblyPath, out factorySharedAssembly);
            var sinkAttributeType = factorySharedAssembly?.GetType(typeof(EventSinkAttribute).FullName ?? "");
            return factoryTypes.Where(f => GetEventSinkType(f, sinkAttributeType) == sinkType);
        }

        public static IEnumerable<Type> GetEventSinkFactories(this MetadataLoadContext loadContext, string assemblyPath, out Assembly? factorySharedAssembly) {
            var factoryTypes = GetEventSinkFactoryTypes(loadContext, assemblyPath, out factorySharedAssembly);
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

        /// <summary>
        /// Get certificate from certificate store based on thumprint or subject common name.
        /// </summary>
        /// <param name="location">Store location.</param>
        /// <param name="thumbprint">Certificate thumprint to look for. Takes precedence over subjectCN when both are specified.</param>
        /// <param name="subjectCN">Subject common name to look for.</param>
        /// <returns>Matching certificate, or <c>null</c> if none was found.</returns>
        public static X509Certificate2? GetCertificate(StoreLocation location, string thumbprint, string subjectCN) {
            if (thumbprint.Length == 0 && subjectCN.Length == 0)
                return null;

            // find matching certificate, use thumbprint if available, otherwise use subject common name (CN)
            using (var store = new X509Store(location)) {
                store.Open(OpenFlags.ReadOnly);
                X509Certificate2? cert = null;
                if (thumbprint.Length > 0) {
                    var certs = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, true);
                    if (certs.Count > 0)
                        cert = certs[0];
                }
                else {
                    var certs = store.Certificates.Find(X509FindType.FindBySubjectName, subjectCN, true);
                    foreach (var matchingCert in certs) {
                        // X509NameType.SimpleName extracts CN from subject (common name)
                        var cn = matchingCert.GetNameInfo(X509NameType.SimpleName, false);
                        if (string.Equals(cn, subjectCN, StringComparison.InvariantCultureIgnoreCase)) {
                            cert = matchingCert;
                            break;
                        }
                    }
                }
                return cert;
            }
        }
    }
}
