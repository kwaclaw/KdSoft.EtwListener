namespace KdSoft.EtwEvents.EventSinks
{
    public class OpenSearchSinkCredentials
    {
        public OpenSearchSinkCredentials() {
        }
        public OpenSearchSinkCredentials(string user, string password) : this() {
            this.User = user;
            this.Password = password;
        }

        public string User { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public string ApiKeyId { get; set; } = string.Empty;

        public string ApiKey { get; set; } = string.Empty;

        // Subject CN for client certificate in LocalMachine store
        public string SubjectCN { get; set; } = string.Empty;
    }
}
