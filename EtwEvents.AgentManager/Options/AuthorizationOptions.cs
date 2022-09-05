
using System.Collections.Generic;
using System;

namespace KdSoft.EtwEvents.AgentManager
{
    public class AuthorizationOptions
    {
        public CertificateValidationOptions ClientValidation { get; set; } = new CertificateValidationOptions();
        public CertificateValidationOptions AgentValidation { get; set; } = new CertificateValidationOptions();
        public HashSet<string> RevokedCertificates { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public string RootCertificateThumbprint { get; set; } = "";
        public int CertExpiryWarningDays { get; set; } = 30;
        public int PendingCertExpiryDays { get; set; } = 7;
        public int PendingCertCheckMinutes { get; set; } = 360;
    }
}
