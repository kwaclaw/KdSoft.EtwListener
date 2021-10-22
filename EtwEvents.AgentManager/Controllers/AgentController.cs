﻿using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
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
    class AgentController: ControllerBase
    {
        readonly AgentProxyManager _agentProxyManager;
        readonly EventSinkService _evtSinkService;
        readonly ILogger<AgentController> _logger;

        public AgentController(
            AgentProxyManager agentProxyManager,
            EventSinkService evtSinkService,
            ILogger<AgentController> logger
        ) {
            this._agentProxyManager = agentProxyManager;
            this._evtSinkService = evtSinkService;
            this._logger = logger;
        }

        #region Server Events for Agent

        async Task<IActionResult> GetMessageEventStream(string agentId, CancellationToken cancelToken) {
            var agentProxy = _agentProxyManager.ActivateProxy(agentId);

            // initial agent state update
            agentProxy.Post(AgentProxyManager.GetStateMessage);

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

        #region Responses from Agent

        IActionResult CompleteResponse(string eventId, string responseJson) {
            var agentId = User.Identity?.Name;
            if (agentId == null)
                return Unauthorized();

            var agentProxy = _agentProxyManager.ActivateProxy(agentId);
            bool success = agentProxy.CompleteResponse(eventId, responseJson);
            if (success)
                return Ok();
            var pd = new ProblemDetails {
                Status = (int)HttpStatusCode.InternalServerError,
                Title = $"Failed to post response for event id: {eventId}.",
            };
            return StatusCode(pd.Status.Value, pd);
        }

        [HttpPost]
        public IActionResult TestFilterResult(string eventId, [FromBody] object buildFilterResult) {
            return CompleteResponse(eventId, buildFilterResult?.ToString() ?? "");
        }

        [HttpPost]
        public IActionResult ApplyFilterResult(string eventId, [FromBody] object buildFilterResult) {
            return CompleteResponse(eventId, buildFilterResult?.ToString() ?? "");
        }

        #endregion

        #region Requests from Agent

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

        public class ModuleRequest
        {
            [Required(AllowEmptyStrings = false)]
            public string SinkType { get; set; } = "";
            [Required(AllowEmptyStrings = false)]
            public string Version { get; set; } = "";
        }

        [HttpPost]
        public IActionResult GetEventSinkModule(ModuleRequest request) {
            var zipFile = _evtSinkService.GetEventSinkZipFile(request.SinkType, request.Version, true);
            if (zipFile == null)
                return NotFound();
            return PhysicalFile(zipFile, "application/zip", Path.GetFileName(zipFile));
        }

        #endregion
    }
}
