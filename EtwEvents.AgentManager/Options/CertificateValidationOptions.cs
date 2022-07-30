namespace KdSoft.EtwEvents.AgentManager
{
    public class CertificateValidationOptions
    {
        public string RootCertificateThumbprint { get; set; } = "";
        public string[] AuthorizedCommonNames { get; set; } = new string[0];
    }

    public class ClientValidationOptions: CertificateValidationOptions { }
    public class AgentValidationOptions: CertificateValidationOptions { }
}
