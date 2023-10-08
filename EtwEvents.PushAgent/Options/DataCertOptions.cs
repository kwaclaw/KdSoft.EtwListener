using System.Security.Cryptography.X509Certificates;

namespace KdSoft.EtwEvents.PushAgent
{
    public class DataCertOptions
    {
        public string Thumbprint { get; set; } = "";
        public StoreLocation Location { get; set; } = StoreLocation.LocalMachine;

        public override bool Equals(object? obj) {
            if (obj is not DataCertOptions other)
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return Thumbprint == other.Thumbprint && Location == other.Location;
        }

        public override int GetHashCode() => Thumbprint.GetHashCode() ^ Location.GetHashCode();
    }
}
