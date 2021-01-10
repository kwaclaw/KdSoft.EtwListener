using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KdSoft.EtwEvents.Client.Shared;
using KdSoft.EtwEvents.WebClient.EventSinks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using shared = KdSoft.EtwEvents.Client.Shared;

namespace KdSoft.EtwEvents.WebClient
{
    [Authorize]
    [ApiController]
    [Route("[controller]/[action]")]
    class EtwController: ControllerBase
    {
        readonly TraceSessionManager _sessionManager;
        readonly EventSinkService _evtSinkService;
        readonly IOptionsMonitor<Models.EventSessionOptions> _optionsMonitor;
        readonly IOptions<Models.ClientCertOptions> _clientCertOptions;
        readonly IOptions<JsonOptions> _jsonOptions;
        readonly IStringLocalizer<EtwController> _;

        public EtwController(
            TraceSessionManager sessionManager,
            EventSinkService evtSinkService,
            IOptionsMonitor<Models.EventSessionOptions> optionsMonitor,
            IOptions<Models.ClientCertOptions> clientCertOptions,
            IOptions<JsonOptions> jsonOptions,
            IStringLocalizer<EtwController> localize
        ) {
            this._sessionManager = sessionManager;
            this._evtSinkService = evtSinkService;
            this._optionsMonitor = optionsMonitor;
            this._clientCertOptions = clientCertOptions;
            this._jsonOptions = jsonOptions;
            this._ = localize;
        }

        //TODO we can specify cert file (incl. key) + password, which should be stored with DataProtection
        // e.g. var clientCertificate = new X509Certificate2(Path.Combine(_certPath, "cert.p12"), "pwd");

        X509Certificate2? GetClientCertificate() {
            string thumbprint = "";
            string subject = "";
            var location = StoreLocation.CurrentUser;

            var currentCert = this.HttpContext.Connection.ClientCertificate;
            var opts = _clientCertOptions.Value;
            if (opts.Thumbprint.Length > 0 || opts.SubjectCN.Length > 0) {
                location = opts.Location;
                thumbprint = opts.Thumbprint;
                subject = opts.SubjectCN;
            }
            else if (currentCert != null) {
                thumbprint = currentCert.Thumbprint;
            }

            return shared.Utils.GetCertificate(location, thumbprint, subject);
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

            var openSessionState = await _sessionManager.OpenSession(request, clientCertificate).ConfigureAwait(false);

            return Ok(openSessionState);
        }

        [HttpPost]
        public async Task<IActionResult> CloseRemoteSession([FromQuery] string name) {
            var success = await _sessionManager.CloseRemoteSession(name).ConfigureAwait(false);
            return Ok(success);
        }

        public const int SessionNotFoundWebSocketStatus = 4901;

        [HttpGet]
        public IActionResult GetEventSinkInfos() {
            var result = _evtSinkService.GetEventSinkInfos();
            return Ok(result);
        }

        async Task<IEventSink> CreateEventSink(Models.EventSinkRequest request, EventSinkHolder holder) {
            IEventSink result;
            switch (request.SinkType) {
                case nameof(DummySink):
                    result = new DummySink(request.Name);
                    break;
                default:
                    var factory = _evtSinkService.LoadEventSinkFactory(request.SinkType);
                    if (factory == null)
                        throw new InvalidOperationException($"Cannot load event sink {request.SinkType}.");
                    var optionsJson = JsonSerializer.Serialize(request.Options);
                    var credentialsJson = JsonSerializer.Serialize(request.Credentials);
                    result = await factory!.Create(request.Name, optionsJson, credentialsJson).ConfigureAwait(false);
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
                var traceSession = await sessionEntry;
                var errorMsg = traceSession.StartEvents(_optionsMonitor);
                await _sessionManager.PostSessionStateChange().ConfigureAwait(false);
                if (errorMsg != null) {
                    return Problem(
                        title: _.GetString("Cannot start session"),
                        instance: nameof(StartEvents),
                        detail: errorMsg
                    );
                }

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

        /// <summary>
        /// Stops events from being delivered.
        /// Since we cannot restart events in a real time session, this API is of limited usefulness.
        /// We can achieve the same result by simply calling CloseRemoteSession().
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> StopEvents(string sessionName) {
            if (_sessionManager.TryGetValue(sessionName, out var sessionEntry)) {
                var traceSession = await sessionEntry;
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
                    var traceSession = await sessionEntry;

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
        public async Task<IActionResult> OpenEventSinks(string sessionName, [FromBody] IEnumerable<Models.EventSinkRequest> sinkRequests) {
            if (_sessionManager.TryGetValue(sessionName, out var sessionEntry)) {
                var traceSession = await sessionEntry;
                var sinkTasks = sinkRequests.Select(sr => CreateEventSink(sr, traceSession.EventSinks));
                var sinkList = await Task.WhenAll(sinkTasks).ConfigureAwait(false);
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
        public async Task<IActionResult> CloseEventSinks(string sessionName, [FromBody] IEnumerable<string> sinkNames) {
            if (_sessionManager.TryGetValue(sessionName, out var sessionEntry)) {
                var traceSession = await sessionEntry;
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
        public async Task<IActionResult> SetCSharpFilter([FromBody] Models.SetFilterRequest request) {
            if (request == null)
                return BadRequest();
            string csharpFilter = string.Empty;  // protobuf does not allow nulls
            if (!string.IsNullOrWhiteSpace(request.CSharpFilter)) {
                csharpFilter = request.CSharpFilter;
            }

            if (_sessionManager.TryGetValue(request.SessionName!, out var sessionEntry)) {
                var traceSession = await sessionEntry;
                var result = await traceSession.SetCSharpFilter(csharpFilter).ConfigureAwait(false);
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
        public async Task<IActionResult> TestCSharpFilter([FromBody] Models.TestFilterRequest request) {
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
