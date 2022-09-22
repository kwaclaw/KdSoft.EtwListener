using System.Collections.Immutable;
using System.Net.Security;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grpc.Core;
using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;

namespace KdSoft.EtwEvents.AgentManager
{
    public class AuthorizationService
    {
        readonly IOptionsMonitor<AuthorizationOptions> _authOpts;
        readonly IWebHostEnvironment _env;
        readonly ILogger<AuthorizationService> _logger;
        readonly object _syncObj = new object();

        static readonly IReadOnlyList<Role> _emptyRoles = new List<Role>().AsReadOnly();
        static readonly ObjectPool<HashSet<Role>> _roleSetPool = new DefaultObjectPool<HashSet<Role>>(new DefaultPooledObjectPolicy<HashSet<Role>>());
        static readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions {
            WriteIndented = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true,
        };
        static readonly JsonNodeOptions _nodeOptions = new JsonNodeOptions {
            PropertyNameCaseInsensitive = true,
        };
        static readonly JsonDocumentOptions _docOptions = new JsonDocumentOptions {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        ImmutableDictionary<string, List<Role>> _roleMap;

        ImmutableDictionary<string, List<Role>> CreateRoleMap(string[] agentNames, string[] managerNames) {
            var builder = ImmutableDictionary.CreateBuilder<string, List<Role>>();
            if (agentNames != null) {
                foreach (var agentName in agentNames) {
                    builder[agentName] = new List<Role> { Role.Agent };
                }
            }
            if (managerNames != null) {
                foreach (var managerName in managerNames) {
                    if (builder.TryGetValue(managerName, out var roleList)) {
                        roleList.Add(Role.Manager);
                    }
                    else {
                        builder[managerName] = new List<Role> { Role.Manager };
                    }
                }
            }
            return builder.ToImmutable();
        }

        public AuthorizationService(IOptionsMonitor<AuthorizationOptions> authOpts, IWebHostEnvironment env, ILogger<AuthorizationService> logger) {
            this._authOpts = authOpts;
            this._env = env;
            this._logger = logger;

            authOpts.OnChange(opts => {
                var newRoleMap = CreateRoleMap(opts.AgentValidation.AuthorizedCommonNames, opts.ClientValidation.AuthorizedCommonNames);
                Interlocked.MemoryBarrier();
                this._roleMap = newRoleMap;
                Interlocked.MemoryBarrier();
            });

            var newRoleMap = CreateRoleMap(authOpts.CurrentValue.AgentValidation.AuthorizedCommonNames, authOpts.CurrentValue.ClientValidation.AuthorizedCommonNames);
            this._roleMap = newRoleMap;
        }

        /// Adds the roles mapped to that userName to the role set.
        /// </summary>
        /// <param name="roleSet"></param>
        /// <param name="userName"></param>
        public void GetRoles(ISet<Role> roleSet, string userName) {
            Interlocked.MemoryBarrier();
            if (this._roleMap.TryGetValue(userName, out var roleList)) {
                foreach (var role in roleList)
                    roleSet.Add(role);
            }
        }

        /// <summary>
        /// Extracts the certificate's role if it is a known role, and adds it to the specified role set.
        /// The certificate therefore maps that role to the user name/identity even if that mapping is not configured.
        /// </summary>
        /// <param name="roleSet"></param>
        /// <param name="cert"></param>
        public static void GetRoles(ISet<Role> roleSet, X509Certificate2 cert) {
            var roles = cert.GetSubjectRoles();
            foreach (var role in roles) {
                switch (role) {
                    case "etw-pushagent":
                        roleSet.Add(Role.Agent);
                        break;
                    case "etw-manager":
                        roleSet.Add(Role.Manager);
                        break;
                    case "etw-admin":
                        roleSet.Add(Role.Admin);
                        break;
                }
            }
        }

        Task<JsonObject?> UpdateRevokedCertName(string thumbprint, string commonName) {
            return Task.Run<JsonObject?>(() => RevokeCertificate(thumbprint, commonName));
        }

        public bool IsCertificateRevoked(X509Certificate2 clientCertificate) {
            Dictionary<string, string>? revokedCerts;
            revokedCerts = this._authOpts.CurrentValue.RevokedCertificates;
            if (revokedCerts != null && revokedCerts.TryGetValue(clientCertificate.Thumbprint, out var commonName)) {
                if (string.IsNullOrWhiteSpace(commonName) || string.Equals(commonName, "Unknown", StringComparison.OrdinalIgnoreCase)) {
                    var certCommonName = clientCertificate.GetNameInfo(X509NameType.SimpleName, false);
                    // reduce probability of multiple identical updates to authorization.json
                    revokedCerts[clientCertificate.Thumbprint] = certCommonName;
                    // update authorization.json
                    _ = UpdateRevokedCertName(clientCertificate.Thumbprint, certCommonName);
                }
                return true;
            }
            return false;
        }

        public JsonObject? RevokeCertificate(string thumbprint, string? commonName) {
            try {
                lock (_syncObj) {
                    var authFile = Path.Combine(_env.ContentRootPath, "authorization.json");
                    var authObj = JsonObject.Parse(File.ReadAllText(authFile), _nodeOptions, _docOptions);
                    var certNode = authObj?["AuthorizationOptions"]?["RevokedCertificates"];
                    if (certNode is JsonObject certObj) {
                        //case-insensitive matching depends on _nodeOptions
                        certObj[thumbprint] = commonName;
                        File.WriteAllText(authFile, authObj?.ToJsonString(_serializerOptions));
                        return certObj;
                    }
                }
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error in {method}.", nameof(RevokeCertificate));
            }
            return null;
        }

        /// <summary>
        /// Returns if the ClaimsPrincipal matches any of the specified roles.
        /// Also adds corresponding claims to the principal's identities.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="roles">Roles to check. Any match means success.</param>
        /// <returns></returns>
        public bool AuthorizePrincipal(CertificateValidatedContext context, params Role[] roles) {
            var clientCertificate = context.ClientCertificate;

            var principal = context.Principal;
            var roleSet = _roleSetPool.Get();
            try {
                roleSet.Clear();
                // primary identity gets role specified in certificate
                GetRoles(roleSet, clientCertificate);
                var primaryIdentity = principal?.Identity as ClaimsIdentity;
                // ClaimsIdentity.Name here is the certificate's Subject Common Name (CN)
                if (primaryIdentity != null) {
                    foreach (var role in roleSet) {
                        primaryIdentity?.AddClaim(new Claim(ClaimTypes.Role, role.ToString()));
                    }
                }
                roleSet.IntersectWith(roles);
                bool success = roleSet.Count > 0;

                var identities = principal?.Identities ?? Enumerable.Empty<ClaimsIdentity>();
                foreach (var identity in identities) {
                    if (identity.Name != null) {
                        roleSet.Clear();
                        GetRoles(roleSet, identity.Name);
                        foreach (var role in roleSet) {
                            identity?.AddClaim(new Claim(ClaimTypes.Role, role.ToString()));
                        }
                        roleSet.IntersectWith(roles);
                        success = success || roleSet.Count > 0;
                    }
                }

                return success;
            }
            finally {
                _roleSetPool.Return(roleSet);
            }
        }

        /// <summary>
        /// Returns if PeerIdentity matches any of the specified roles.
        /// </summary>
        /// <param name="peerIdentity"></param>
        /// <param name="clientCertificate"></param>
        /// <param name="roles">Roles to check. Any match means success.</param>
        /// <returns></returns>
        public bool AuthorizePeerIdentity(IEnumerable<AuthProperty> peerIdentity, ServerCallContext context, params Role[] roles) {
            var httpContext = context.GetHttpContext();
            var clientCertificate = httpContext?.Connection.ClientCertificate;
            if (clientCertificate is null)
                return false;

            var roleSet = _roleSetPool.Get();
            try {
                roleSet.Clear();
                GetRoles(roleSet, clientCertificate);
                foreach (var authProp in peerIdentity) {
                    if (string.IsNullOrEmpty(authProp.Value))
                        continue;
                    GetRoles(roleSet, authProp.Value);
                }
                roleSet.IntersectWith(roles);
                return roleSet.Count > 0;
            }
            finally {
                _roleSetPool.Return(roleSet);
            }
        }

        //public void GetRoles(string identity, ISet<Role> roleSet) {
        //    // get roles configured for user
        //    var roles = GetRoles(identity);
        //    foreach (var role in roles) {
        //        roleSet.Add(role);
        //    }
        //}

        //public HashSet<Role> GetRoles(X509Certificate2 clientCert, IEnumerable<string> identities) {
        //    var roleSet = new HashSet<Role>();
        //    // ClaimsIdentity.Name here is the certificate's Subject Common Name (CN)
        //    foreach (var identity in identities) {
        //        if (!string.IsNullOrEmpty(identity)) {
        //            // get role from certificate
        //            string? certRole = clientCert.GetSubjectRole()?.ToLowerInvariant();
        //            if (certRole != null) {
        //                if (certRole.Equals("etw-pushagent")) {
        //                    roleSet.Add(Role.Agent);
        //                }
        //                else if (certRole.Equals("etw-manager")) {
        //                    roleSet.Add(Role.Manager);
        //                }
        //            }
        //            // get roles configured for user
        //            var roles = GetRoles(identity);
        //            foreach (var role in roles) {
        //                roleSet.Add(role);
        //            }
        //        }
        //    }
        //    return roleSet;
        //}

        public bool ValidateClientCertificate(X509Certificate2 cert, X509Chain? chain, SslPolicyErrors errors) {
            if (errors != SslPolicyErrors.None) {
                return false;
            }
            if (chain is null) {
                if (IsCertificateRevoked(cert)) {
                    return false;
                }
            }
            else {
                // if a root certificate thumbprint is specified then we accept only certificates that are derived from it
                var rootThumbprint = this._authOpts.CurrentValue.RootCertificateThumbprint?.ToUpperInvariant();
                bool rootValidation = !string.IsNullOrEmpty(rootThumbprint);
                bool rootValidated = false;
                foreach (var chainElement in chain.ChainElements) {
                    if (IsCertificateRevoked(chainElement.Certificate)) {
                        return false;
                    }
                    if (rootValidation && chainElement.Certificate.Thumbprint.ToUpperInvariant() == rootThumbprint) {
                        rootValidated = true;
                    }
                }
                if (rootValidation && !rootValidated) {
                    return false;
                }
            }
            return true;
        }
    }

    public enum Role
    {
        Agent,
        Manager,
        Admin
    }
}
