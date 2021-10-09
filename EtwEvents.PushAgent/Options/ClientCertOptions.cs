using System.Security.Cryptography.X509Certificates;

namespace KdSoft.EtwEvents.PushAgent
{
    public class ClientCertOptions
    {
        public string Thumbprint { get; set; } = "";
        public string SubjectCN { get; set; } = "";
        public StoreLocation Location { get; set; } = StoreLocation.CurrentUser;
    }
}
