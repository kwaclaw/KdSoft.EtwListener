using System.Security.Cryptography.X509Certificates;

namespace KdSoft.EtwEvents.PushAgent
{
    public class ClientCertOptions
    {
        public string SubjectCN { get; set; } = "";
        public string SubjectRole { get; set; } = "";
        public StoreLocation Location { get; set; } = StoreLocation.CurrentUser;

        public override bool Equals(object? obj) {
            if (obj is not ClientCertOptions other)
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return SubjectCN == other.SubjectCN && SubjectRole == other.SubjectRole && Location == other.Location;
        }

        public override int GetHashCode() => SubjectCN.GetHashCode() ^ SubjectRole.GetHashCode() ^ Location.GetHashCode();
    }
}
