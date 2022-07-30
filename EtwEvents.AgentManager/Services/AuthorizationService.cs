﻿using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Grpc.Core;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;

namespace KdSoft.EtwEvents.AgentManager
{
    public class AuthorizationService
    {
        readonly IOptionsMonitor<ClientValidationOptions> _clientOpts;
        readonly IOptionsMonitor<AgentValidationOptions> _agentOpts;
        static readonly IReadOnlyList<Role> _emptyRoles = new List<Role>().AsReadOnly();
        static readonly ObjectPool<HashSet<Role>> _roleSetPool = new DefaultObjectPool<HashSet<Role>>(new DefaultPooledObjectPolicy<HashSet<Role>>());

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

        public AuthorizationService(IOptionsMonitor<ClientValidationOptions> clientOpts, IOptionsMonitor<AgentValidationOptions> agentOpts) {
            this._clientOpts = clientOpts;
            this._agentOpts = agentOpts;

            clientOpts.OnChange(opts => {
                var newRoleMap = CreateRoleMap(this._agentOpts.CurrentValue.AuthorizedCommonNames, opts.AuthorizedCommonNames);
                Interlocked.MemoryBarrier();
                this._roleMap = newRoleMap;
                Interlocked.MemoryBarrier();
            });

            agentOpts.OnChange(opts => {
                var newRoleMap = CreateRoleMap(opts.AuthorizedCommonNames, this._clientOpts.CurrentValue.AuthorizedCommonNames);
                Interlocked.MemoryBarrier();
                this._roleMap = newRoleMap;
                Interlocked.MemoryBarrier();
            });

            var newRoleMap = CreateRoleMap(this._agentOpts.CurrentValue.AuthorizedCommonNames, this._clientOpts.CurrentValue.AuthorizedCommonNames);
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
        public static void GetRole(ISet<Role> roleSet, X509Certificate2 cert) {
            string? certRole = cert.GetSubjectRole()?.ToLowerInvariant();
            if (certRole != null) {
                if (certRole.Equals("etw-pushagent")) {
                    roleSet.Add(Role.Agent);
                }
                else if (certRole.Equals("etw-manager")) {
                    roleSet.Add(Role.Manager);
                }
            }
        }

        /// <summary>
        /// Returns if the ClaimsPrincipal matches any of the specified roles.
        /// Also adds corresponding claims to the principal's identities.
        /// </summary>
        /// <param name="principal"></param>
        /// <param name="clientCertificate"></param>
        /// <returns></returns>
        public bool AuthorizePrincipal(ClaimsPrincipal principal, X509Certificate2 clientCertificate, params Role[] roles) {
            var roleSet = _roleSetPool.Get();
            try {
                roleSet.Clear();
                // primary identity gets role specified in certificate
                GetRole(roleSet, clientCertificate);
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
        /// <param name="roles"></param>
        /// <returns></returns>
        public bool AuthorizePeerIdentity(IEnumerable<AuthProperty> peerIdentity, X509Certificate2 clientCertificate, params Role[] roles) {
            var roleSet = _roleSetPool.Get();
            try {
                roleSet.Clear();
                GetRole(roleSet, clientCertificate);
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
    }

    public enum Role
    {
        Agent,
        Manager
    }
}
