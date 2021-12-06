using System;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using KdSoft.EtwEvents.AgentManager.Services;
using KdSoft.EtwLogging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace KdSoft.EtwEvents.AgentManager
{
    [Authorize(Roles = "Manager")]
    [ApiController]
    [Route("[controller]/[action]")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    class ManagerController: ControllerBase
    {
        readonly AgentProxyManager _agentProxyManager;
        readonly EventSinkService _evtSinkService;
        readonly IOptions<JsonOptions> _jsonOptions;
        readonly JsonFormatter _jsonFormatter;
        readonly ILogger<ManagerController> _logger;

        public ManagerController(
            AgentProxyManager agentProxyManager,
            EventSinkService evtSinkService,
            JsonFormatter jsonFormatter,
            IOptions<JsonOptions> jsonOptions,
            ILogger<ManagerController> logger
        ) {
            this._agentProxyManager = agentProxyManager;
            this._evtSinkService = evtSinkService;
            this._jsonFormatter = jsonFormatter;
            this._jsonOptions = jsonOptions;
            this._logger = logger;
        }

        [HttpGet]
        public IActionResult GetEventSinkInfos() {
            var result = _evtSinkService.GetEventSinkInfos().Select(si => si.Item1);
            return Ok(result);
        }

        #region Server Events for Manager

        //see https://stackoverflow.com/questions/36227565/aspnet-core-server-sent-events-response-flush
        //also, maybe better: https://github.com/tpeczek/Lib.AspNetCore.ServerSentEvents and https://github.com/tpeczek/Demo.AspNetCore.ServerSentEvents

        async Task<IActionResult> GetAgentStateEventStream(CancellationToken cancelToken) {
            var resp = Response;

            resp.ContentType = Constants.EventStreamHeaderValue;
            resp.Headers[HeaderNames.CacheControl] = "no-cache,no-store";
            resp.Headers[HeaderNames.Pragma] = "no-cache";
            // hopefully prevents buffering
            resp.Headers[HeaderNames.ContentEncoding] = "identity";

            // Make sure we disable all response buffering for SSE
            var bufferingFeature = resp.HttpContext.Features.Get<IHttpResponseBodyFeature>();
            bufferingFeature?.DisableBuffering();

            var jsonSerializerOptions = _jsonOptions.Value.JsonSerializerOptions;

            var changes = _agentProxyManager.GetAgentStateChanges();
            await foreach (var change in changes.WithCancellation(cancelToken).ConfigureAwait(false)) {
                var statusJson = System.Text.Json.JsonSerializer.Serialize(change, jsonSerializerOptions);
                await resp.WriteAsync($"data:{statusJson}\n\n", cancelToken).ConfigureAwait(false);
                await resp.Body.FlushAsync(cancelToken).ConfigureAwait(false);
            }

            // OkResult not right here, tries to set status code which is not allowed once the response has started
            return new EmptyResult();
        }

        [HttpGet]
        public async Task<IActionResult> GetAgentStates(CancellationToken cancelToken) {
            var req = Request;
            if (req.Headers[HeaderNames.Accept].Contains(Constants.EventStreamHeaderValue)) {
                return await GetAgentStateEventStream(cancelToken).ConfigureAwait(false);
            }
            else {
                var change = await _agentProxyManager.GetAgentStates().ConfigureAwait(false);
                return Ok(change);
            }
        }

        #endregion

        #region Messages to Agent

        IActionResult PostAgent(string agentId, string eventName, string jsonData) {
            ProblemDetails pd;
            if (_agentProxyManager.TryGetProxy(agentId, out var proxy)) {
                var evt = new ControlEvent {
                    Id = proxy.GetNextEventId().ToString(),
                    Event = eventName,
                    Data = jsonData
                };

                if (proxy.Post(evt)) {
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

        async Task<IActionResult> CallAgent(string agentId, string eventName, string jsonData, TimeSpan timeout) {
            ProblemDetails pd;
            if (_agentProxyManager.TryGetProxy(agentId, out var proxy)) {
                var eventId = proxy.GetNextEventId().ToString();
                var evt = new ControlEvent {
                    Id = eventId,
                    Event = eventName,
                    Data = jsonData
                };

                //TODO configure response timeout externally

                var cts = new CancellationTokenSource(timeout);
                try {
                    var resultJson = await proxy.CallAsync(eventId, evt, cts.Token).ConfigureAwait(false);
                    return Content(resultJson, new MediaTypeHeaderValue("application/json"));
                }
                catch (Exception ex) {
                    //TODO handle exception types, like cancellation due to timeout
                    pd = new ProblemDetails {
                        Status = (int)HttpStatusCode.InternalServerError,
                        Title = ex.Message,
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

        [HttpPost]
        public IActionResult Start(string agentId) {
            return PostAgent(agentId, "Start", "");
        }

        [HttpPost]
        public IActionResult Stop(string agentId) {
            return PostAgent(agentId, "Stop", "");
        }

        [HttpPost]
        public IActionResult UpdateProviders(string agentId, [FromBody] JsonElement enabledProviders) {
            // we are passing the JSON simply through, enabledProviders should match protobuf message ProviderSettingsList
            return PostAgent(agentId, "UpdateProviders", enabledProviders.GetRawText() ?? "{}");
        }

        /// <summary>
        /// Dynamic filter parts passed from SPA client to agent.
        /// </summary>
        /// <param name="agentId">Agent Id</param>
        /// <param name="filterObj">JSON object like:
        /// {
        ///   "dynamicParts": [
        ///     "int num = 0;"
        ///     "return num > 0;"
        ///   ]
        /// } 
        /// </param>
        [HttpPost]
        public Task<IActionResult> TestFilter(string agentId, [FromBody] JsonElement filterObj) {
            var dynamicParts = filterObj.GetProperty("dynamicParts").EnumerateArray().Select(dp => dp.GetString()).ToImmutableArray();
            // WE are supplying the filter template
            var filter = FilterHelper.MergeFilterTemplate(dynamicParts);
            var json = _jsonFormatter.Format(filter);
            return CallAgent(agentId, "TestFilter", json, TimeSpan.FromSeconds(15));
        }

        /// <summary>
        /// Processing options passed from SPA client to agent.
        /// </summary>
        /// <param name="agentId">Agent Id</param>
        /// <param name="optionsObj">JSON object like:
        /// {
        ///   "batchSize": 100,
        ///   "maxWriteDelayMSecs": 400,
        ///   "dynamicParts": [
        ///     "int num = 0;"
        ///     "return num > 0;"
        ///   ]
        /// } 
        /// </param>
        [HttpPost]
        public Task<IActionResult> ApplyProcessingOptions(string agentId, [FromBody] JsonElement optionsObj) {
            var dynamicParts = optionsObj.GetProperty("dynamicParts").EnumerateArray().Select(dp => dp.GetString()).ToImmutableArray();
            // WE are supplying the filter template
            var filter = FilterHelper.MergeFilterTemplate(dynamicParts);
            var processingOptions = new ProcessingOptions {
                BatchSize = optionsObj.GetProperty("batchSize").GetInt32(),
                MaxWriteDelayMSecs = optionsObj.GetProperty("maxWriteDelayMSecs").GetInt32(),
                Filter = filter
            };
            var json = _jsonFormatter.Format(processingOptions);
            return CallAgent(agentId, "ApplyProcessingOptions", json, TimeSpan.FromSeconds(15));
        }

        [HttpPost]
        public IActionResult UpdateEventSink(string agentId, [FromBody] JsonNode eventSinkProfile) {
            // The Credentials and Options properties need to be converted back to JSON

            var jsonSettings = _jsonOptions.Value.JsonSerializerOptions;

            var opts = eventSinkProfile["options"];
            var optsString = opts?.ToJsonString(jsonSettings);
            eventSinkProfile["options"] = optsString;

            var creds = eventSinkProfile["credentials"];
            var credsString = creds?.ToJsonString();
            eventSinkProfile["credentials"] = credsString;

            var profileJson = eventSinkProfile.ToJsonString(jsonSettings);
            return PostAgent(agentId, "UpdateEventSink", profileJson ?? "");
        }

        #endregion
    }
}
