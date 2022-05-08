using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using KdSoft.EtwLogging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace KdSoft.EtwEvents.AgentManager
{
    [Authorize(Roles = "Agent")]
    [ApiController]
    [Route("[controller]/[action]")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    class AgentController: ControllerBase
    {
        readonly AgentProxyManager _agentProxyManager;
        readonly EventSinkProvider _evtSinkProvider;
        readonly JsonFormatter _jsonFormatter;
        readonly ILogger<AgentController> _logger;

        public AgentController(
            AgentProxyManager agentProxyManager,
            EventSinkProvider evtSinkProvider,
            JsonFormatter jsonFormatter,
            ILogger<AgentController> logger
        ) {
            this._agentProxyManager = agentProxyManager;
            this._evtSinkProvider = evtSinkProvider;
            this._jsonFormatter = jsonFormatter;
            this._logger = logger;
        }

        #region Server Events for Agent

        async Task<IActionResult> GetMessageEventStream(string agentId, CancellationToken cancelToken) {
            var agentProxy = _agentProxyManager.ActivateProxy(agentId);
            // on initial EventSource (SSE) connect we store the Uri and client certificate information
            // (used in the HTTP request) in the AgentProxy instance, for later use in configuring a gRPCSink
            agentProxy.ManagerUri = $"{Request.Scheme}://{Request.Host}";
            var certThumprint = User.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.Thumbprint)?.Value ?? "";
            agentProxy.ClientCertThumbprint = certThumprint;
            var certDistName = User.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.X500DistinguishedName)?.Value ?? "";
            agentProxy.ClientCertDN = certDistName;

            var emptyFilter = Filter.MergeFilterTemplate();
            var emptyFilterEvent = new ControlEvent {
                Event = Constants.SetEmptyFilterEvent,
                Id = agentProxy.GetNextEventId().ToString(),
                Data = _jsonFormatter.Format(emptyFilter)
            };
            agentProxy.Post(emptyFilterEvent);

            // initial agent state update
            agentProxy.Post(AgentProxyManager.GetStateMessage);

            var finished = await agentProxy.ProcessMessages(Response, cancelToken).ConfigureAwait(false);
            if (finished)
                _logger.LogInformation("Agent closed connection: {agentId}", agentId);
            else
                _logger.LogInformation("Agent was disconnected: {agentId}", agentId);

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
                    _logger.LogInformation("Agent connected: {agentId}", agentId);
                }
                else {
                    _logger.LogInformation("Agent reconnected: {agentId} - Last event id: {lastEventId}", agentId, lastEventId);
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
        public IActionResult TestFilterResult(string eventId, [FromBody] JsonElement buildFilterResult) {
            return CompleteResponse(eventId, buildFilterResult.GetRawText() ?? "{}");
        }

        [HttpPost]
        public IActionResult ApplyAgentOptionsResult(string eventId, [FromBody] JsonElement applyResult) {
            // passing the JSON right through
            return CompleteResponse(eventId, applyResult.GetRawText() ?? "{}");
        }

        #endregion

        #region Requests from Agent

        [HttpPost]
        public async Task<IActionResult> UpdateState([FromBody] JsonElement stateObj) {
            var agentId = User.Identity?.Name;
            if (agentId == null)
                return Unauthorized();

            var state = AgentState.Parser.WithDiscardUnknownFields(true).ParseJson(stateObj.GetRawText());

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
            var zipFile = _evtSinkProvider.GetEventSinkZipFile(request.SinkType, request.Version, true);
            if (zipFile == null)
                return NotFound();
            return PhysicalFile(zipFile, "application/zip", Path.GetFileName(zipFile));
        }

        #endregion
    }
}
