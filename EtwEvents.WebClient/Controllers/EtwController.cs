using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using KdSoft.EtwEvents.WebClient.EventSinks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace KdSoft.EtwEvents.WebClient
{
    [Authorize]
    [ApiController]
    [Route("[controller]/[action]")]
    class EtwController: ControllerBase
    {
        readonly TraceSessionManager _sessionManager;
        readonly IOptionsMonitor<Models.EventSessionOptions> _optionsMonitor;
        readonly IOptions<Models.ClientCertOptions> _clientCertOptions;
        readonly IOptions<JsonOptions> _jsonOptions;
        readonly IStringLocalizer<EtwController> _;

        public EtwController(
            TraceSessionManager sessionManager,
            IOptionsMonitor<Models.EventSessionOptions> optionsMonitor,
            IOptions<Models.ClientCertOptions> clientCertOptions,
            IOptions<JsonOptions> jsonOptions,
            IStringLocalizer<EtwController> localize
        ) {
            this._sessionManager = sessionManager;
            this._optionsMonitor = optionsMonitor;
            this._clientCertOptions = clientCertOptions;
            this._jsonOptions = jsonOptions;
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
        public async Task<IActionResult> OpenSession(Models.TraceSessionRequest request) {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var clientCertificate = GetClientCertificate();
            if (clientCertificate == null)
                return Problem(
                    title: _.GetString("Authentication failure"),
                    instance: nameof(OpenSession),
                    detail: _.GetString("Cannot find matching client certificate")
                );

            var openSessionState = await _sessionManager.OpenSession(
                request.Name, request.Host, clientCertificate, request.Providers, request.LifeTime.ToDuration()
            ).ConfigureAwait(false);

            return Ok(openSessionState);
        }

        [HttpPost]
        public async Task<IActionResult> CloseRemoteSession([FromQuery]string name) {
            var success = await _sessionManager.CloseRemoteSession(name).ConfigureAwait(false);
            return Ok(success);
        }

        public const int SessionNotFoundWebSocketStatus = 4901;

        IEventSink CreateEventSink(Models.EventSinkRequest request, EventSinkHolder holder) {
            IEventSink result;
            var jsonSerializerOptions = _jsonOptions.Value.JsonSerializerOptions;
            switch (request.SinkType) {
                case nameof(EventSinks.MongoSink):
                    var optsElement = (JsonElement)request.Options;
                    var sinkOptions = optsElement.ToObject<MongoSinkOptions>(jsonSerializerOptions);
                    var credsElement = (JsonElement)request.Credentials;
                    result = new EventSinks.MongoSink(
                        request.Name,
                        sinkOptions,
                        credsElement.GetProperty("database").GetString(),
                        credsElement.GetProperty("user").GetString(),
                        credsElement.GetProperty("password").GetString(),
                        CancellationToken.None);
                    break;
                case nameof(EventSinks.DummySink):
                default:
                    result = new EventSinks.DummySink(request.Name);
                    break;

            }
            AddEventSink(result, holder);
            return result;
        }

        void AddEventSink(IEventSink sink, EventSinkHolder holder) {
            holder.AddEventSink(sink);
            ConfigureEventSinkClosure(sink, holder);
        }

        Task ConfigureEventSinkClosure(IEventSink sink, EventSinkHolder holder) {
            return sink.RunTask.ContinueWith(async rt => {
                try {
                    if (!rt.Result) { // was not disposed
                        await sink.DisposeAsync().ConfigureAwait(false);
                    }
                    holder.RemoveEventSink(sink);
                    await _sessionManager.PostSessionStateChange().ConfigureAwait(false);
                }
                catch (Exception ex) {
                    //_logger.LogError(ex);
                }
            }, TaskScheduler.Default);
        }

        [HttpGet]
        public async Task<IActionResult> StartEvents(string sessionName) {
            if (_sessionManager.TryGetValue(sessionName, out var sessionEntry)) {
                var traceSession = await sessionEntry.SessionTask.ConfigureAwait(false);
                var eventSessionTask = traceSession.StartEvents(_optionsMonitor);
                await _sessionManager.PostSessionStateChange().ConfigureAwait(false);

                // If this is a WebSocket request we add a WebSocket sink
                if (HttpContext.WebSockets.IsWebSocketRequest) {
                    var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);

                    // this WebSocketSink has a life-cycle tied to the EventSession, while other sinks can be added and removed dynamically
                    var webSocketName = Guid.NewGuid().ToString();
                    var webSocketSink = new EventSinks.WebSocketSink(webSocketName, webSocket);
                    try {
                        // must initialize before configuring disposal
                        await webSocketSink.Initialize(CancellationToken.None).ConfigureAwait(false);
                        AddEventSink(webSocketSink, traceSession.EventSinks);

                        // wait for receive task to terminate
                        await webSocketSink.RunTask.ConfigureAwait(false);
                    }
                    finally {
                        await webSocketSink.DisposeAsync().ConfigureAwait(false);
                    }

                    // OkResult not right here, tries to set status code which is not good in this scenario
                    return new EmptyResult();
                }
                else {
                    return Ok();
                }
            }
            else {
                return Problem(
                    title: _.GetString("Session not found"),
                    instance: nameof(StartEvents),
                    detail: _.GetString("Session may have been closed already.")
                );
            }
        }

        [HttpPost]
        public async Task<IActionResult> StopEvents(string sessionName) {
            if (_sessionManager.TryGetValue(sessionName, out var sessionEntry)) {
                var traceSession = await sessionEntry.SessionTask.ConfigureAwait(false);
                await traceSession.StopEvents().ConfigureAwait(false);
                await _sessionManager.PostSessionStateChange().ConfigureAwait(false);
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

        [HttpGet]
        public async Task<IActionResult> ObserveEvents(string sessionName) {
            if (HttpContext.WebSockets.IsWebSocketRequest) {
                var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
                if (_sessionManager.TryGetValue(sessionName, out var sessionEntry)) {
                    var traceSession = await sessionEntry.SessionTask.ConfigureAwait(false);

                    // the WebSocketSink has a life-cycle tied to the EventSession, while other sinks can be added and removed dynamically
                    var webSocketName = Guid.NewGuid().ToString();
                    var webSocketSink = new EventSinks.WebSocketSink(webSocketName, webSocket);
                    try {
                        // must initialize before configuring disposal
                        await webSocketSink.Initialize(CancellationToken.None).ConfigureAwait(false);
                        AddEventSink(webSocketSink, traceSession.EventSinks);
                        await _sessionManager.PostSessionStateChange().ConfigureAwait(false);

                        // wait for receive task to terminate
                        await webSocketSink.RunTask.ConfigureAwait(false);
                    }
                    finally {
                        await webSocketSink.DisposeAsync().ConfigureAwait(false);
                    }

                    // OkResult not right here, tries to set status code which is not good in this scenario
                    return new EmptyResult();
                }
                else {
                    // Returning HTTP status codes does not work with WebSockets, we need to close
                    // the WebSocket again with a custom status code in the range 40000 - 4999
                    await webSocket.CloseAsync
                        ((WebSocketCloseStatus)SessionNotFoundWebSocketStatus,
                        _.GetString("Session not found"),
                        CancellationToken.None
                    ).ConfigureAwait(false);
                    // OkResult not right here, tries to set status code which is not good in this scenario
                    return new EmptyResult();
                }
            }
            else {
                return BadRequest();
            }
        }

        [HttpPost]
        public async Task<IActionResult> OpenEventSinks(string sessionName, [FromBody]IEnumerable<Models.EventSinkRequest> sinkRequests) {
            if (_sessionManager.TryGetValue(sessionName, out var sessionEntry)) {
                var traceSession = await sessionEntry.SessionTask.ConfigureAwait(false);
                var sinkList = sinkRequests.Select(sr => CreateEventSink(sr, traceSession.EventSinks));
                traceSession.EventSinks.AddEventSinks(sinkList);
                await _sessionManager.PostSessionStateChange().ConfigureAwait(false);
                return Ok();
            }
            else {
                return Problem(
                    title: _.GetString("Session not found"),
                    instance: nameof(OpenEventSinks),
                    detail: _.GetString("Session may have been closed already.")
                );
            }
        }

        [HttpPost]
        public async Task<IActionResult> CloseEventSinks(string sessionName, [FromBody]IEnumerable<string> sinkNames) {
            if (_sessionManager.TryGetValue(sessionName, out var sessionEntry)) {
                var traceSession = await sessionEntry.SessionTask.ConfigureAwait(false);
                var closedSinks = traceSession.EventSinks.RemoveEventSinks(sinkNames);
                var disposeTasks = closedSinks.Select(sink => sink.DisposeAsync()).ToArray();
                foreach (var disposeTask in disposeTasks) {
                    await disposeTask.ConfigureAwait(false);
                }
                await _sessionManager.PostSessionStateChange().ConfigureAwait(false);
                return Ok();
            }
            else {
                return Problem(
                    title: _.GetString("Session not found"),
                    instance: nameof(CloseEventSinks),
                    detail: _.GetString("Session may have been closed already.")
                );
            }
        }

        const string EventStreamHeaderValue = "text/event-stream";

        //see https://stackoverflow.com/questions/36227565/aspnet-core-server-sent-events-response-flush
        //also, maybe better: https://github.com/tpeczek/Lib.AspNetCore.ServerSentEvents and https://github.com/tpeczek/Demo.AspNetCore.ServerSentEvents

        async Task<IActionResult> GetSessionStateEventStream(CancellationToken cancelToken) {
            var resp = Response;
            resp.Headers.Add("Cache-Control-Type", "no-cache");
            resp.Headers.Add("Content-Type", EventStreamHeaderValue);

            var jsonSerializerOptions = _jsonOptions.Value.JsonSerializerOptions;

            var changes = _sessionManager.GetSessionStateChanges();
            await foreach (var change in changes.WithCancellation(cancelToken)) {
                var statusJson = System.Text.Json.JsonSerializer.Serialize(change, jsonSerializerOptions);
                await resp.WriteAsync($"data:{statusJson}\n\n").ConfigureAwait(false);
                await resp.Body.FlushAsync().ConfigureAwait(false);
            }

            // OkResult not right here, tries to set status code which is not allowed once the response has started
            return new EmptyResult();
        }

        [HttpGet]
        public async Task<IActionResult> GetSessionStates(CancellationToken cancelToken) {
            var req = Request;
            if (req.Headers["Accept"].Equals(EventStreamHeaderValue)) {
                return await GetSessionStateEventStream(cancelToken).ConfigureAwait(false);
            }
            else {
                var change = await _sessionManager.GetSessionStates().ConfigureAwait(false);
                return Ok(change);
            }
        }

        [HttpPost]
        public async Task<IActionResult> SetCSharpFilter([FromBody]Models.SetFilterRequest request) {
            if (request == null)
                return BadRequest();
            string csharpFilter = string.Empty;  // protobuf does not allow nulls
            if (!string.IsNullOrWhiteSpace(request.CSharpFilter)) {
                csharpFilter = request.CSharpFilter;
            }

            if (_sessionManager.TryGetValue(request.SessionName!, out var sessionEntry)) {
                var session = await sessionEntry.SessionTask.ConfigureAwait(false);
                var result = await session.SetCSharpFilter(csharpFilter).ConfigureAwait(false);
                await _sessionManager.PostSessionStateChange().ConfigureAwait(false);
                return Ok(result);
            }
            else {
                return Problem(
                    title: _.GetString("Session not found"),
                    instance: nameof(SetCSharpFilter),
                    detail: _.GetString("Session may have been closed already.")
                );
            }
        }

        [HttpPost]
        public async Task<IActionResult> TestCSharpFilter([FromBody]Models.TestFilterRequest request) {
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
