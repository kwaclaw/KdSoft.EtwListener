using System.Security.Cryptography.X509Certificates;

namespace EtwEvents.WebClient.Models
{
    public class ClientCertOptions
    {
        public string Thumbprint { get; set; } = "";
        public StoreLocation Location { get; set; } = StoreLocation.CurrentUser;
    }
}
