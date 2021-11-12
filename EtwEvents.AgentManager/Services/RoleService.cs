using System.Collections.Generic;

namespace KdSoft.EtwEvents.AgentManager
{
    public class RoleService
    {
        readonly Dictionary<string, List<Role>> _roleMap;
        static readonly IReadOnlyList<Role> _emptyRoles = new List<Role>().AsReadOnly();

        public RoleService(ISet<string>? agentNames, ISet<string>? clientNames) {
            this._roleMap = new Dictionary<string, List<Role>>();
            if (agentNames != null) {
                foreach (var agentName in agentNames) {
                    this._roleMap[agentName] = new List<Role> { Role.Agent };
                }
            }
            if (clientNames != null) {
                foreach (var clientName in clientNames) {
                    if (this._roleMap.TryGetValue(clientName, out var roleList)) {
                        roleList.Add(Role.Manager);
                    }
                    else {
                        this._roleMap[clientName] = new List<Role> { Role.Manager };
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
    }

    public enum Role
    {
        Agent,
        Manager
    }
}
