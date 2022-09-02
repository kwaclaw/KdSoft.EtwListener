
namespace KdSoft.EtwEvents.AgentManager
{
    public class AuthorizationOptions
    {
        public CertificateValidationOptions ClientValidation { get; set; } = new CertificateValidationOptions();
        public CertificateValidationOptions AgentValidation { get; set; } = new CertificateValidationOptions();
        public string RootCertificateThumbprint { get; set; } = "";
        public int CertExpiryWarningDays { get; set; } = 30;
        public int PendingCertExpiryDays { get; set; } = 7;
        public int PendingCertCheckMinutes { get; set; } = 360;
    }
}
