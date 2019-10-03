using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EtwEvents.WebClient;
using EtwEvents.WebClient.Models;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using KdSoft.EtwLogging;
using Microsoft.AspNetCore.Mvc;

namespace EtwEvents.WebClient
{
    [ApiController]
    [Route("[controller]/[action]")]
    public class EtwController: ControllerBase
    {
        readonly TraceSessionManager _sessionManager;

        public EtwController(TraceSessionManager sessionManager) {
            this._sessionManager = sessionManager;
        }

        [HttpPost]
        public async Task<IActionResult> OpenSession(TraceSessionRequest request) {
            var credentials = ChannelCredentials.Insecure;
            try {
                var session = await _sessionManager.OpenSession(request.Name, request.Host, credentials, request.Providers, request.LifeTime.ToDuration()).ConfigureAwait(false);
                return Ok(new SessionResult(session.EnabledProviders, session.FailedProviders));
            }
            catch (Exception ex) {
                return Problem(title: ex.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> StartEvents(string sessionName) {
            if (HttpContext.WebSockets.IsWebSocketRequest) {
                var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
                if (_sessionManager.TryGetValue(sessionName, out var sessionEntry)) {
                    var session = await sessionEntry.CreateTask.ConfigureAwait(false);
                    session.StartEvents(webSocket);
                    return new OkResult();
                }
                else {
                    return new NotFoundObjectResult(new { Error = "Session not found" });
                }
            }
            else {
                return new BadRequestResult();
            }
        }

        [HttpPost]
        public async Task<IActionResult> StopEvents(string sessionName) {
            if (_sessionManager.TryGetValue(sessionName, out var sessionEntry)) {
                var session = await sessionEntry.CreateTask.ConfigureAwait(false);
                await session.StopEvents().ConfigureAwait(false);
                return new OkResult();
            }
            else {
                return new NotFoundObjectResult(new { Error = "Session not found" });
            }
        }
    }
}
