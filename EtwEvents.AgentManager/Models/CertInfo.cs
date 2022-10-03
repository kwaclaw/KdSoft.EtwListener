namespace KdSoft.EtwEvents.AgentManager
{
    public class CertInfo
    {
        public string Thumbprint { get; set; } = "";
        public string? Name { get; set; } = null; // Subject CN (Common Name)
    }
}
