using System.Collections.Generic;
using System.Security.Claims;

namespace KdSoft.EtwEvents.WebClient
{
    public class AuthService
    {
        ISet<string> _authorizedNames;

        public AuthService(ISet<string> authorizedNames) {
            this._authorizedNames = authorizedNames;
        }

        public bool IsAuthorized(ClaimsPrincipal principal) {
            if (principal == null)
                return false;
            foreach (var identity in principal.Identities) {
                if (!identity.IsAuthenticated)
                    continue;
                if (_authorizedNames.Contains(identity.Name))
                    return true;
            }
            return false;
        }
    }
}
