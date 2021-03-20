using System;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using KdSoft.EtwEvents.AgentManager.Services;
using KdSoft.EtwEvents.PushAgent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;

namespace KdSoft.EtwEvents.AgentManager.Controllers {
    [Authorize(Roles = "Manager")]
    [ApiController]
    [Route("[controller]/[action]")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public class ManagerController: ControllerBase {
        readonly AgentProxyManager _agentProxyManager;
        readonly ILogger _logger;

        int _agentEventId;

        public ManagerController(
            AgentProxyManager agentProxyManager,
            ILogger logger
        ) {
            this._agentProxyManager = agentProxyManager;
            this._logger = logger;
        }


        #region Server Events for Manager

        async Task<IActionResult> GetMessageEventStream(string agentId, CancellationToken cancelToken) {
            var queue = _agentProxyManager.ActivateProxy(agentId);

            var finished = await queue.ProcessMessages(Response, cancelToken).ConfigureAwait(false);
            if (finished)
                _logger.LogInformation($"Finished SSE connection: {agentId}");
            else
                _logger.LogInformation($"Cancelled SSE connection: {agentId}");

            // OkResult not right here, tries to set status code which is not allowed once the response has started
            return new EmptyResult();
        }

        // returns Base91 encoded string (uses characters 0x21-0x7E, except for 0x2D, 0x5C and 0x27)

        [HttpGet]
        public async Task<IActionResult> GetEvents([FromQuery] string agentId, CancellationToken cancelToken) {
            var lastEventId = Request.Headers["Last-Event-ID"].ToString();

            var acceptHeaders = Request.Headers[HeaderNames.Accept];
            if (acceptHeaders.Count == 0 || acceptHeaders.Contains(AgentProxy.EventStreamHeaderValue) || acceptHeaders.Contains(Constants.TextWildCardHeaderValue)) {
                if (string.IsNullOrEmpty(lastEventId)) {
                    _logger.LogInformation($"Connected SSE: {agentId}");
                }
                else {
                    _logger.LogInformation($"Reconnected SSE: {agentId} - Last event id: {lastEventId}");
                }

                return await GetMessageEventStream(agentId, cancelToken).ConfigureAwait(false);
            }
            var pd = new ProblemDetails {
                Status = (int)HttpStatusCode.NotAcceptable,
                Title = "Only accept event stream requests.",
            };
            return StatusCode(pd.Status.Value, pd);
        }

        #endregion

        #region Messages to Agent

        public IActionResult PostMessage(string agentId, string eventName, string jsonData) {
            ProblemDetails pd;
            if (_agentProxyManager.TryGetProxy(agentId, out var proxy)) {
                var evt = new ControlEvent {
                    Id = Interlocked.Increment(ref _agentEventId).ToString(),
                    Event = eventName,
                    Data = jsonData
                };

                if (proxy.Writer.TryWrite(evt)) {
                    return Ok();
                }
                else {
                    pd = new ProblemDetails {
                        Status = (int)HttpStatusCode.InternalServerError,
                        Title = "Could not post message.",
                    };
                    return StatusCode(pd.Status.Value, pd);
                }
            }
            pd = new ProblemDetails {
                Status = (int)HttpStatusCode.NotFound,
                Title = "Agent does not exist.",
            };
            return StatusCode(pd.Status.Value, pd);
        }

        #endregion
    }
}
