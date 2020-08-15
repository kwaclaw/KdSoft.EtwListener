namespace KdSoft.EtwEvents.EventSinks
{
    public class MongoSinkCredentials
    {
        public MongoSinkCredentials() {
        }
        public MongoSinkCredentials(string database, string user, string password) : this() {
            this.Database = database;
            this.User = user;
            this.Password = password;
        }

        public string Database { get; set; } = string.Empty;

        public string User { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;
    }
}
