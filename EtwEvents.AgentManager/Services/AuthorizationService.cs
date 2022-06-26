using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

namespace KdSoft.EtwEvents.AgentManager
{
    public class AuthorizationService
    {
        readonly Dictionary<string, List<Role>> _roleMap;
        static readonly IReadOnlyList<Role> _emptyRoles = new List<Role>().AsReadOnly();

        public AuthorizationService(ISet<string>? agentNames, ISet<string>? managerNames) {
            this._roleMap = new Dictionary<string, List<Role>>();
            if (agentNames != null) {
                foreach (var agentName in agentNames) {
                    this._roleMap[agentName] = new List<Role> { Role.Agent };
                }
            }
            if (managerNames != null) {
                foreach (var managerName in managerNames) {
                    if (this._roleMap.TryGetValue(managerName, out var roleList)) {
                        roleList.Add(Role.Manager);
                    }
                    else {
                        this._roleMap[managerName] = new List<Role> { Role.Manager };
                    }
                }
            }
        }

        public IReadOnlyList<Role> GetRoles(string userName) {
            if (this._roleMap.TryGetValue(userName, out var roleList)) {
                return roleList;
            }
            return _emptyRoles;
        }

        public HashSet<Role> GetRoles(X509Certificate2 clientCert, IEnumerable<string> identities) {
            var roleSet = new HashSet<Role>();
            // ClaimsIdentity.Name here is the certificate's Subject Common Name (CN)
            foreach (var identity in identities) {
                if (!string.IsNullOrEmpty(identity)) {
                    // get role from certificate
                    string? certRole = clientCert.GetSubjectRole()?.ToLowerInvariant();
                    if (certRole != null) {
                        if (certRole.Equals("etw-pushagent")) {
                            roleSet.Add(Role.Agent);
                        }
                        else if (certRole.Equals("etw-manager")) {
                            roleSet.Add(Role.Manager);
                        }
                    }
                    // get roles configured for user
                    var roles = GetRoles(identity);
                    foreach (var role in roles) {
                        roleSet.Add(role);
                    }
                }
            }
            return roleSet;
        }
    }

    public enum Role
    {
        Agent,
        Manager
    }
}
