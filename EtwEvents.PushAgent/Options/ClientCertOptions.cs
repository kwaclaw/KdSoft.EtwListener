using System.Security.Cryptography.X509Certificates;

namespace KdSoft.EtwEvents.PushAgent
{
    public class ClientCertOptions
    {
        public string Thumbprint { get; set; } = "";
        public string SubjectCN { get; set; } = "";
        public StoreLocation Location { get; set; } = StoreLocation.CurrentUser;

        public override bool Equals(object? obj) {
            var other = obj as ClientCertOptions;
            if (other == null)
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return Thumbprint == other.Thumbprint && SubjectCN == other.SubjectCN && Location == other.Location;
        }

        public override int GetHashCode() => Thumbprint.GetHashCode() ^ SubjectCN.GetHashCode() ^ Location.GetHashCode();
    }
}
