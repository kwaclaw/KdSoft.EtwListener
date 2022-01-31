using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using KdSoft.EtwEvents.EventSinks;
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
        readonly EventSinkProvider _evtSinkProvider;
        readonly IOptions<JsonOptions> _jsonOptions;
        readonly JsonFormatter _jsonFormatter;
        readonly ILogger<ManagerController> _logger;

        public ManagerController(
            AgentProxyManager agentProxyManager,
            EventSinkProvider evtSinkProvider,
            JsonFormatter jsonFormatter,
            IOptions<JsonOptions> jsonOptions,
            ILogger<ManagerController> logger
        ) {
            this._agentProxyManager = agentProxyManager;
            this._evtSinkProvider = evtSinkProvider;
            this._jsonFormatter = jsonFormatter;
            this._jsonOptions = jsonOptions;
            this._logger = logger;
        }

        [HttpGet]
        public IActionResult GetEventSinkInfos() {
            var result = _evtSinkProvider.GetEventSinkInfos().Select(si => si.Item1);
            return Ok(result);
        }

        HttpResponse SetupSSEResponse() {
            var resp = Response;

            resp.ContentType = Constants.EventStreamHeaderValue;
            resp.Headers[HeaderNames.CacheControl] = "no-cache,no-store";
            resp.Headers[HeaderNames.Pragma] = "no-cache";
            // hopefully prevents buffering
            resp.Headers[HeaderNames.ContentEncoding] = "identity";

            // Make sure we disable all response buffering for SSE
            var bufferingFeature = resp.HttpContext.Features.Get<IHttpResponseBodyFeature>();
            bufferingFeature?.DisableBuffering();

            return resp;
        }

        #region Agent Events to Manager (SPA)

        //see https://stackoverflow.com/questions/36227565/aspnet-core-server-sent-events-response-flush
        //also, maybe better: https://github.com/tpeczek/Lib.AspNetCore.ServerSentEvents and https://github.com/tpeczek/Demo.AspNetCore.ServerSentEvents

        async Task<IActionResult> GetAgentStateEventStream(CancellationToken cancelToken) {
            var resp = SetupSSEResponse();

            var jsonSerializerOptions = _jsonOptions.Value.JsonSerializerOptions;

            var changes = _agentProxyManager.GetAgentStateChanges();
            await foreach (var change in changes.WithCancellation(cancelToken).ConfigureAwait(false)) {
                var statusJson = JsonSerializer.Serialize(change, jsonSerializerOptions);
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

        #region Control Messages to Agent

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
            return PostAgent(agentId, Constants.StartEvent, "");
        }

        [HttpPost]
        public IActionResult Stop(string agentId) {
            return PostAgent(agentId, Constants.StopEvent, "");
        }

        [HttpPost]
        public IActionResult GetState(string agentId) {
            return PostAgent(agentId, Constants.GetStateEvent, "");
        }

        [HttpPost]
        public IActionResult UpdateProviders(string agentId, [FromBody] JsonElement enabledProviders) {
            // we are passing the JSON simply through, enabledProviders should match protobuf message ProviderSettingsList
            return PostAgent(agentId, Constants.UpdateProvidersEvent, enabledProviders.GetRawText() ?? "{}");
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
            var filter = dynamicParts.Length == 0
                ? new EtwLogging.Filter()  // we are clearing the filter
                : Filter.MergeFilterTemplate(dynamicParts); // WE are supplying the filter template
            var json = _jsonFormatter.Format(filter);
            return CallAgent(agentId, Constants.TestFilterEvent, json, TimeSpan.FromSeconds(15));
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
            var filter = dynamicParts.Length == 0
                ? new EtwLogging.Filter()  // we are clearing the filter
                : Filter.MergeFilterTemplate(dynamicParts); // WE are supplying the filter template
            var processingOptions = new ProcessingOptions {
                Filter = filter
            };
            var json = _jsonFormatter.Format(processingOptions);
            return CallAgent(agentId, Constants.ApplyProcessingOptionsEvent, json, TimeSpan.FromSeconds(15));
        }

        [HttpPost]
        public IActionResult UpdateEventSinks(string agentId, [FromBody] JsonArray eventSinkProfiles) {
            var jsonSerializerOptions = _jsonOptions.Value.JsonSerializerOptions;

            var profilesHolder = new List<JsonNode>();
            // The credentials and options properties need to be converted back to JSON
            foreach (var eventSinkProfile in eventSinkProfiles) {
                if (eventSinkProfile == null)
                    continue;

                var opts = eventSinkProfile["options"];
                var optsString = opts?.ToJsonString(jsonSerializerOptions);
                eventSinkProfile["options"] = optsString;

                var creds = eventSinkProfile["credentials"];
                var credsString = creds?.ToJsonString();
                eventSinkProfile["credentials"] = credsString;

                profilesHolder.Add(eventSinkProfile);
            }

            // build EventSinkProfiles message (protobuf)
            var profilesMessage = new JsonObject();
            var profiles = new JsonObject();
            profilesMessage["profiles"] = profiles;

            // a JsonArray owns its nodes, so we must first remove them before we can add them to the JsonObject
            eventSinkProfiles.Clear();
            foreach (var eventSinkProfile in profilesHolder) {
                profiles[(string?)eventSinkProfile["name"] ?? "unknown"] = eventSinkProfile;
            }

            var profilesMessageJson = profilesMessage.ToJsonString(jsonSerializerOptions);
            return PostAgent(agentId, Constants.UpdateEventSinksEvent, profilesMessageJson ?? "");
        }

        #endregion

        #region ETW Events to Manager (SPA)

        async Task<IActionResult> GetEtwEventStream(AgentProxy proxy, CancellationToken cancelToken) {
            var receivingSource = new TaskCompletionSource<int>();
            var etwEventStream = await proxy.GetEtwEventStream(receivingSource).ConfigureAwait(false);
            if (etwEventStream == null) {
                receivingSource.TrySetCanceled();
                return new EmptyResult();
            }

            int eventCount = 0;
            try {
                var resp = SetupSSEResponse();
                while (await etwEventStream.MoveNext(cancelToken).ConfigureAwait(false)) {
                    var evtBatch = etwEventStream.Current;
                    eventCount += evtBatch.Events.Count();

                    var evtBatchJson = evtBatch.ToString();
                    await resp.WriteAsync($"data:{evtBatchJson}\n\n", cancelToken).ConfigureAwait(false);
                    await resp.Body.FlushAsync(cancelToken).ConfigureAwait(false);
                }
                receivingSource.TrySetResult(eventCount);
            }
            catch (Exception ex) {
                receivingSource.TrySetException(ex);
            }

            // OkResult not right here, tries to set status code which is not allowed once the response has started
            return new EmptyResult();
        }

        [HttpGet]
        public async Task<IActionResult> GetEtwEvents(CancellationToken cancelToken) {
            var agentId = User.Identity?.Name;
            if (agentId == null)
                return Unauthorized();

            var proxy = _agentProxyManager.ActivateProxy(agentId);

            // send control message to Agent, telling it to activate the proper event sink
            var evt = new ControlEvent {
                Id = proxy.GetNextEventId().ToString(),
                Event = Constants.StartManagerSinkEvent,
                //TODO Data = jsonData ?
            };

            if (!proxy.Post(evt)) {
                var pd = new ProblemDetails {
                    Status = (int)HttpStatusCode.InternalServerError,
                    Title = "Could not post message.",
                };
                return StatusCode(pd.Status.Value, pd);
            }

            if (Request.Headers[HeaderNames.Accept].Contains(Constants.EventStreamHeaderValue)) {
                return await GetEtwEventStream(proxy, cancelToken).ConfigureAwait(false);
            }

            return BadRequest();
        }

        #endregion
    }
}
