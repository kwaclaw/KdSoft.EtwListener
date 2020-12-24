using System.Security.Cryptography.X509Certificates;

namespace KdSoft.EtwEvents.WebClient.Models
{
    public class ClientCertOptions
    {
        public string Thumbprint { get; set; } = "";
        public string SubjectCN { get; set; } = "";
        public StoreLocation Location { get; set; } = StoreLocation.CurrentUser;
    }
}
