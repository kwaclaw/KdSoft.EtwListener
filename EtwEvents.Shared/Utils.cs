using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

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

        /// <summary>
        /// Get certificate from certificate store based on thumprint or subject common name.
        /// If multiple certificates match then the one with the newest NotBefore date is returned.
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
                        cert = certs.OrderByDescending(cert => cert.NotBefore).First();
                }
                else {
                    var certs = store.Certificates.Find(X509FindType.FindBySubjectName, subjectCN, true);
                    foreach (var matchingCert in certs) {
                        // X509NameType.SimpleName extracts CN from subject (common name)
                        var cn = matchingCert.GetNameInfo(X509NameType.SimpleName, false);
                        if (string.Equals(cn, subjectCN, StringComparison.InvariantCultureIgnoreCase)) {
                            if (cert == null)
                                cert = matchingCert;
                            else if (cert.NotBefore < matchingCert.NotBefore)
                                cert = matchingCert;
                        }
                    }
                }
                return cert;
            }
        }

        // it seems that the same DN component can be encoded by OID or OID's friendly name, e.g. "OID.2.5.4.72" or "role"
        public static Regex SubjectRoleRegex = new Regex(@"(OID\.2\.5\.4\.72|\.2\.5\.4\.72|role)\s*=\s*(?<role>[^,=]*)\s*(,|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static string? GetSubjectRole(this X509Certificate2 cert) {
            string? certRole = null;
            var match = Utils.SubjectRoleRegex.Match(cert.Subject);
            if (match.Success) {
                certRole = match.Groups["role"].Value;
            }
            return certRole;
        }

        /// <summary>
        /// Get certificates from certificate store based on application policy OID and predicate callback.
        /// The resulting collections is ordered by descending NotBefore date.
        /// </summary>
        /// <param name="location">Store location.</param>
        /// <param name="policyOID">Application policy OID to look for, e.g. Client Authentication (1.3.6.1.5.5.7.3.2). Required.</param>
        /// <param name="predicate">Callback to check certificate against a condition. Optional.</param>
        /// <returns>Matching certificates, or an empty collection if none are found.</returns>
        public static IEnumerable<X509Certificate2> GetCertificates(StoreLocation location, string policyOID, Predicate<X509Certificate2> predicate) {
            if (policyOID.Length == 0)
                return Enumerable.Empty<X509Certificate2>();

            using (var store = new X509Store(location)) {
                store.Open(OpenFlags.ReadOnly);
                var certs = store.Certificates.Find(X509FindType.FindByApplicationPolicy, policyOID, true);
                if (certs.Count == 0 || predicate == null)
                    return certs.OrderByDescending(cert => cert.NotBefore);
                return certs.Where(crt => predicate(crt)).OrderByDescending(cert => cert.NotBefore);
            }
        }
    }
}
