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
    public class ManagerController: ControllerBase {
        readonly AgentProxyManager _agentProxyManager;
        readonly IOptions<JsonOptions> _jsonOptions;
        readonly ILogger _logger;

        int _agentEventId;

        public ManagerController(
            AgentProxyManager agentProxyManager,
            IOptions<JsonOptions> jsonOptions,
            ILogger logger
        ) {
            this._agentProxyManager = agentProxyManager;
            this._jsonOptions = jsonOptions;
            this._logger = logger;
        }


        #region Server Events for Manager

        const string EventStreamHeaderValue = "text/event-stream";

        //see https://stackoverflow.com/questions/36227565/aspnet-core-server-sent-events-response-flush
        //also, maybe better: https://github.com/tpeczek/Lib.AspNetCore.ServerSentEvents and https://github.com/tpeczek/Demo.AspNetCore.ServerSentEvents

        async Task<IActionResult> GetAgentStateEventStream(CancellationToken cancelToken) {
            var resp = Response;

            resp.ContentType = EventStreamHeaderValue;
            resp.Headers[HeaderNames.CacheControl] = "no-cache, no-transform";
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
            if (req.Headers[HeaderNames.Accept].Contains(EventStreamHeaderValue)) {
                return await GetAgentStateEventStream(cancelToken).ConfigureAwait(false);
            }
            else {
                var change = await _agentProxyManager.GetAgentStates().ConfigureAwait(false);
                return Ok(change);
            }
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
