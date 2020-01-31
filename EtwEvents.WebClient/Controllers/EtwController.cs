using System;
using System.Net;
using System.Resources;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using EtwEvents.WebClient.Models;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Localization;

namespace EtwEvents.WebClient
{
    [Authorize]
    [ApiController]
    [Route("[controller]/[action]")]
    public class EtwController: ControllerBase
    {
        readonly TraceSessionManager _sessionManager;
        readonly IOptionsMonitor<EventSessionOptions> _optionsMonitor;
        readonly IOptions<ClientCertOptions> _clientCertOptions;
        readonly IStringLocalizer<EtwController> _;

        public EtwController(
            TraceSessionManager sessionManager,
            IOptionsMonitor<EventSessionOptions> optionsMonitor,
            IOptions<ClientCertOptions> clientCertOptions,
            IStringLocalizer<EtwController> localize
        ) {
            this._sessionManager = sessionManager;
            this._optionsMonitor = optionsMonitor;
            this._clientCertOptions = clientCertOptions;
            this._ = localize;
        }

        //TODO we can specify cert file (incl. key) + password, which should be stored with DataProtection
        // e.g. var clientCertificate = new X509Certificate2(Path.Combine(_certPath, "cert.p12"), "pwd");

        X509Certificate2? GetClientCertificate() {
            string thumbprint = "";
            StoreLocation location = StoreLocation.CurrentUser;

            var currentCert = this.HttpContext.Connection.ClientCertificate;
            if (_clientCertOptions.Value.Thumbprint.Length > 0) {
                thumbprint = _clientCertOptions.Value.Thumbprint;
                location = _clientCertOptions.Value.Location;
            }
            else if (currentCert != null) {
                thumbprint = currentCert.Thumbprint;
            }

            if (thumbprint.Length == 0)
                return null;

            using (var store = new X509Store(location)) {
                store.Open(OpenFlags.ReadOnly);
                var certs = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, true);
                if (certs.Count == 0)
                    return null;
                return certs[0];
            }
        }

        [HttpPost]
        public async Task<IActionResult> OpenSession(TraceSessionRequest request) {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var clientCertificate = GetClientCertificate();
            if (clientCertificate == null)
                return Problem(
                    title: _.GetString("Authentication failure"),
                    instance: nameof(OpenSession),
                    detail: _.GetString("Cannot find matching client certificate")
                );

            var session = await _sessionManager.OpenSession(request.Name, request.Host, clientCertificate, request.Providers, request.LifeTime.ToDuration()).ConfigureAwait(false);
            return Ok(new SessionResult(session.EnabledProviders, session.RestartedProviders));
        }

        [HttpPost]
        public async Task<IActionResult> CloseRemoteSession([FromQuery]string name) {
            var success = await _sessionManager.CloseRemoteSession(name).ConfigureAwait(false);
            return Ok(success);
        }

        [HttpGet]
        public async Task<IActionResult> StartEvents(string sessionName) {
            if (HttpContext.WebSockets.IsWebSocketRequest) {
                if (_sessionManager.TryGetValue(sessionName, out var sessionEntry)) {
                    var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
                    var session = await sessionEntry.CreateTask.ConfigureAwait(false);
                    await session.StartEvents(webSocket, _optionsMonitor).ConfigureAwait(false);
                    return new EmptyResult();  // OkResult not right here, tries to set status code which is not good in this scenario
                }
                else {
                	//TODO  should we not return a WebSocket close status?
                    return Problem(
                        statusCode: (int)HttpStatusCode.NotFound,
                        title: _.GetString("Session not found"),
                        instance: nameof(StartEvents),
                        detail: _.GetString("Session may have been closed already.")
                    );
                }
            }
            else {
                return BadRequest();
            }
        }

        [HttpPost]
        public async Task<IActionResult> StopEvents(string sessionName) {
            if (_sessionManager.TryGetValue(sessionName, out var sessionEntry)) {
                var session = await sessionEntry.CreateTask.ConfigureAwait(false);
                await session.StopEvents().ConfigureAwait(false);
                return Ok();
            }
            else {
                return Problem(
                    title: _.GetString("Session not found"),
                    instance: nameof(StopEvents),
                    detail: _.GetString("Session may have been closed already.")
                );
            }
        }

        [HttpPost]
        public async Task<IActionResult> SetCSharpFilter([FromBody]SetFilterRequest request) {
            if (request == null)
                return BadRequest();
            string csharpFilter = string.Empty;  // protobuf does not allow nulls
            if (!string.IsNullOrWhiteSpace(request.CSharpFilter)) {
                csharpFilter = request.CSharpFilter;
            }

            if (_sessionManager.TryGetValue(request.SessionName!, out var sessionEntry)) {
                var session = await sessionEntry.CreateTask.ConfigureAwait(false);
                var result = await session.SetCSharpFilter(csharpFilter).ConfigureAwait(false);
                return Ok(result);
            }
            else {
                return Problem(title: "Session not found", instance: nameof(SetCSharpFilter), detail: "Session may have been closed already.");
            }
        }

        [HttpPost]
        public async Task<IActionResult> TestCSharpFilter([FromBody]TestFilterRequest request) {
            if (request == null)
                return BadRequest();
            if (string.IsNullOrWhiteSpace(request.Host))
                return BadRequest("Host must be specified.");

            var clientCertificate = GetClientCertificate();
            if (clientCertificate == null)
                return Problem(
                    title: _.GetString("Authentication failure"),
                    instance: nameof(TestCSharpFilter),
                    detail: _.GetString("Cannot find matching client certificate")
                );

            string csharpFilter = string.Empty;  // protobuf does not allow nulls
            if (!string.IsNullOrWhiteSpace(request.CSharpFilter)) {
                csharpFilter = request.CSharpFilter;
            }
            var result = await TraceSession.TestCSharpFilter(request.Host, clientCertificate, csharpFilter).ConfigureAwait(false);
            return Ok(result);
        }
    }
}
