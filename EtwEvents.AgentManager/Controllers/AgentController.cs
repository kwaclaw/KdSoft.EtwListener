using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using KdSoft.EtwEvents.AgentManager.Services;
using KdSoft.EtwEvents.PushAgent.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace KdSoft.EtwEvents.AgentManager.Controllers
{
    [Authorize(Roles = "Agent")]
    [ApiController]
    [Route("[controller]/[action]")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public class AgentController: ControllerBase
    {
        readonly AgentProxyManager _agentProxyManager;
        readonly ILogger<AgentController> _logger;

        public AgentController(
            AgentProxyManager agentProxyManager,
            ILogger<AgentController> logger
        ) {
            this._agentProxyManager = agentProxyManager;
            this._logger = logger;
        }

        #region Server Events for Agent

        async Task<IActionResult> GetMessageEventStream(string agentId, CancellationToken cancelToken) {
            var agentProxy = _agentProxyManager.ActivateProxy(agentId);

            // initial agent state update
            agentProxy.Writer.TryWrite(AgentProxyManager.GetStateMessage);

            var finished = await agentProxy.ProcessMessages(Response, cancelToken).ConfigureAwait(false);
            if (finished)
                _logger.LogInformation($"Finished SSE connection: {agentId}");
            else
                _logger.LogInformation($"Cancelled SSE connection: {agentId}");

            await _agentProxyManager.PostAgentStateChange().ConfigureAwait(false);

            // OkResult not right here, tries to set status code which is not allowed once the response has started
            return new EmptyResult();
        }

        [HttpGet]
        public async Task<IActionResult> GetEvents(CancellationToken cancelToken) {
            var agentId = User.Identity?.Name;
            if (agentId == null)
                return Unauthorized();

            var lastEventId = Request.Headers["Last-Event-ID"].ToString();

            var acceptHeaders = Request.Headers[HeaderNames.Accept];
            if (acceptHeaders.Count == 0 || acceptHeaders.Contains(Constants.EventStreamHeaderValue)) {
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

        #region Updates from Agent

        [HttpPost]
        public async Task<IActionResult> UpdateState(AgentState state) {
            var agentId = User.Identity?.Name;
            if (agentId == null)
                return Unauthorized();

            var agentProxy = _agentProxyManager.ActivateProxy(agentId);
            // AgentState.ID must always match the authenticated identity
            state.Id = agentId;
            agentProxy.SetState(state);

            await _agentProxyManager.PostAgentStateChange().ConfigureAwait(false);
            return Ok();
        }

        #endregion
    }
}
