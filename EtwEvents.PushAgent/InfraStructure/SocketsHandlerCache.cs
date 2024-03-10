using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;

namespace KdSoft.EtwEvents.PushAgent
{
    /// <summary>
    /// Caches <see cref="SocketsHttpHandler"/> instance until explicitly invalidated.
    /// Recreates it on next access. Required to change client certificates on the fly.
    /// </summary>
    class SocketsHandlerCache
    {
        readonly IOptionsMonitor<ControlOptions> _options;

        public SocketsHandlerCache(IOptionsMonitor<ControlOptions> options) {
            this._options = options;
            _handler = CreateHandler();
        }

        SocketsHttpHandler CreateHandler() {
            var clientCerts = Utils.GetClientCertificates(_options.CurrentValue.ClientCertificate);
            if (clientCerts.Count == 0) {
                var defaultCertOpts = new ClientCertOptions { SubjectCN = "kd-soft-default-client", Location = StoreLocation.LocalMachine };
                clientCerts = Utils.GetClientCertificates(defaultCertOpts);
                if (clientCerts.Count == 0)
                    throw new ArgumentException("Cannot find the default client certificate.");
            }
            return Utils.CreateHttpHandler(clientCerts.ToArray());
        }

        int invalid = 0;
        public bool Refresh() {
            return Interlocked.Exchange(ref invalid, 99) == 0;
        }

        SocketsHttpHandler _handler;
        public SocketsHttpHandler Handler {
            get {
                var oldInvalid = Interlocked.Exchange(ref invalid, 0);
                if (oldInvalid != 0)
                    return _handler = CreateHandler();
                return _handler;
            }
        }
    }
}
