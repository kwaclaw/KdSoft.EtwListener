namespace KdSoft.EtwEvents.EventSinks
{
    public class MongoSinkCredentials
    {
        public MongoSinkCredentials() { }

        public MongoSinkCredentials(string database, string user, string password) : this() {
            this.Database = database;
            this.User = user;
            this.Password = password;
        }

        public MongoSinkCredentials(string database, string certificateCommonName) : this() {
            this.Database = database;
            this.CertificateCommonName = certificateCommonName;
        }

        public string Database { get; set; } = string.Empty;

        public string User { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public string CertificateCommonName { get; set; } = string.Empty;
    }
}
