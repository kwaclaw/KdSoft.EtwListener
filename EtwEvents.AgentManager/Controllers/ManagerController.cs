using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using KdSoft.EtwEvents.AgentManager.Services;
using KdSoft.EtwEvents.PushAgent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace KdSoft.EtwEvents.AgentManager.Controllers
{
    [Authorize(Roles = "Manager")]
    [ApiController]
    [Route("[controller]/[action]")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public class ManagerController: ControllerBase
    {
        readonly AgentProxyManager _agentProxyManager;
        readonly IOptions<JsonOptions> _jsonOptions;
        readonly ILogger<ManagerController> _logger;
        //readonly JsonFormatter _jsonFormatter;

        int _agentEventId;

        public ManagerController(
            AgentProxyManager agentProxyManager,
            IOptions<JsonOptions> jsonOptions,
            ILogger<ManagerController> logger
        ) {
            this._agentProxyManager = agentProxyManager;
            this._jsonOptions = jsonOptions;
            this._logger = logger;
            //var jsonSettings = JsonFormatter.Settings.Default.WithFormatDefaultValues(true).WithFormatEnumsAsIntegers(true);
            //_jsonFormatter = new JsonFormatter(jsonSettings);
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
                    Id = Interlocked.Increment(ref _agentEventId).ToString(),
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
                var eventId = Interlocked.Increment(ref _agentEventId).ToString();
                var evt = new ControlEvent {
                    Id = eventId,
                    Event = eventName,
                    Data = jsonData
                };

                //TODO configure response timeout
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
        public IActionResult UpdateProviders(string agentId, [FromBody] object enabledProviders) {
            // we are passing the JSON simply through, enabledProviders should match protobuf message ProviderSettingsList
            return PostAgent(agentId, "UpdateProviders", enabledProviders?.ToString() ?? "");
        }

        [HttpPost]
        public Task<IActionResult> TestFilter(string agentId, [FromBody] object filterRequest) {
            // we are passing the JSON simply through, filterRequest should match protobuf message TestFilterRequest
            return CallAgent(agentId, "TestFilter", filterRequest?.ToString() ?? "", TimeSpan.FromSeconds(15));
        }

        [HttpPost]
        public Task<IActionResult> ApplyFilter(string agentId, [FromBody] object filterRequest) {
            // we are passing the JSON simply through, filterRequest should match protobuf message TestFilterRequest
            return CallAgent(agentId, "ApplyFilter", filterRequest?.ToString() ?? "", TimeSpan.FromSeconds(15));
        }

        #endregion
    }
}
