﻿using System;
using System.Net.Http;
using System.Threading;
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
            if (clientCerts.Count == 0)
                throw new ArgumentException("Cannot find a client certificate based on specified options.", nameof(_options.CurrentValue.ClientCertificate));
            var handler = Utils.CreateHttpHandler(clientCerts.ToArray());
            return handler;
        }

        int invalid = 0;
        public bool Invalidate() {
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
