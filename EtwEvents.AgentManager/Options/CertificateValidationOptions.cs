using System;
using System.Collections.Generic;

namespace KdSoft.EtwEvents.AgentManager
{
    public class CertificateValidationOptions
    {
        public string[] AuthorizedCommonNames { get; set; } = new string[0];
        public HashSet<string> RevokedCertificates { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }
}
