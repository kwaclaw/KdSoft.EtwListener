using System.Buffers;
using System.Collections.Immutable;
using System.Net;
using System.Net.Mime;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Google.Protobuf;
using KdSoft.EtwEvents.EventSinks;
using KdSoft.EtwLogging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
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
        readonly IStringLocalizer<ManagerController> _;
        readonly AgentProxyManager _agentProxyManager;
        readonly EventSinkProvider _evtSinkProvider;
        readonly AuthorizationService _authService;
        readonly CertificateFileService _agentCertFileService;
        readonly IOptions<JsonOptions> _jsonOptions;
        readonly IOptionsMonitor<AuthorizationOptions> _authOpts;
        readonly JsonFormatter _jsonFormatter;
        readonly ILogger<ManagerController> _logger;

        public ManagerController(
            IStringLocalizer<ManagerController> localizer,
            AgentProxyManager agentProxyManager,
            EventSinkProvider evtSinkProvider,
            AuthorizationService authService,
            CertificateFileService agentCertFileService,
            JsonFormatter jsonFormatter,
            IOptions<JsonOptions> jsonOptions,
            IOptionsMonitor<AuthorizationOptions> authOpts,
            ILogger<ManagerController> logger
        ) {
            this._ = localizer;
            this._agentProxyManager = agentProxyManager;
            this._evtSinkProvider = evtSinkProvider;
            this._authService = authService;
            this._agentCertFileService = agentCertFileService;
            this._jsonFormatter = jsonFormatter;
            this._jsonOptions = jsonOptions;
            this._authOpts = authOpts;
            this._logger = logger;
        }

        string UserInfo() {
            var ident = this.User.Identity;
            var name = ident?.Name ?? "anonymous";
            var thumbPrintClaim = this.User.FindFirst(ClaimTypes.Thumbprint);
            var thumbPrint = thumbPrintClaim != null ? $" ({thumbPrintClaim.Value})" : string.Empty;
            return $"[{name}{thumbPrint}]";
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

        [HttpGet]
        public IActionResult GetEventSinkInfos() {
            var result = _evtSinkProvider.GetEventSinkInfos().Select(si => si.Item1);
            return Ok(result);
        }

        #region Certificates

        [HttpPost]
        public async Task<IActionResult> UploadAgentCerts([FromForm] IFormCollection model, CancellationToken cancelToken) {
            if (model is null || model.Files is null) {
                _logger.LogError("Error in {method}, files not specified.", nameof(UploadAgentCerts));
                var problemDetails = new ProblemDetails {
                    Status = (int)HttpStatusCode.BadRequest,
                    Instance = null,
                    Title = "Invalid arguments",
                };
                return StatusCode(problemDetails.Status.Value, problemDetails);
            }

            StringBuilder? sb = null;
            foreach (var formFile in model.Files) {
                try {
                    await _agentCertFileService.SaveAsync(formFile, cancelToken).ConfigureAwait(false);
                }
                catch (Exception ex) {
                    sb ??= new StringBuilder();
                    sb.AppendLine($"\t{formFile.FileName}");
                    _logger.LogError(ex, "Error saving uploaded file {file}.", formFile.FileName);
                }
            }
            if (sb is not null) {
                var problemDetails = new ProblemDetails {
                    Status = (int)HttpStatusCode.InternalServerError,
                    Instance = null,
                    Title = "Could not save some files:",
                    Detail = sb.ToString()
                };
                return StatusCode(problemDetails.Status.Value, problemDetails);
            }
            return Ok();
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public IActionResult GetRevokedCerts() {
            var revokedList = _authOpts.CurrentValue.RevokedCertificates
                .Select(rv => new CertInfo { Name = rv.Value, Thumbprint = rv.Key })
                .OrderBy(ci => ci.Name);
            return Ok(revokedList);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public IActionResult RevokeCert([FromBody] CertInfo cert) {
            var revokedCerts = _authService.RevokeCertificate(cert.Thumbprint, cert.Name);
            if (revokedCerts is null) {
                var problemDetails = new ProblemDetails {
                    Status = (int)HttpStatusCode.InternalServerError,
                    Instance = null,
                    Title = "Could not revoke certificate.",
                };
                return StatusCode(problemDetails.Status.Value, problemDetails);
            }
            var revokedList = revokedCerts
                .Select(rv => new CertInfo { Name = rv.Value?.ToString(), Thumbprint = rv.Key })
                .OrderBy(ci => ci.Name);
            return Ok(revokedList);
        }

        [HttpPost]
        public ObjectResult GetCertInfo([FromForm] FileModel model, CancellationToken cancelToken) {
            if (model is null || model.File is null) {
                _logger.LogError("Error in {method}, file not specified.", nameof(GetCertInfo));
                var problemDetails = new ProblemDetails {
                    Status = (int)HttpStatusCode.BadRequest,
                    Instance = null,
                    Title = "Invalid arguments",
                };
                return StatusCode(problemDetails.Status.Value, problemDetails);
            }

            X509Certificate2 cert;
            try {
                byte[] certBytes;
                using (var rs = model.File.OpenReadStream()) {
                    checked {
                        var len = (int)rs.Length;
                        certBytes = new byte[len];
                        rs.Read(certBytes, 0, len);
                    }
                }
                cert = CertUtils.LoadCertificate(certBytes);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error in {method}.", nameof(GetCertInfo));
                var problemDetails = new ProblemDetails {
                    Status = (int)HttpStatusCode.InternalServerError,
                    Instance = null,
                    Title = "Could not load certificate.",
                };
                return StatusCode(problemDetails.Status.Value, problemDetails);
            }

            var certCommonName = cert.GetNameInfo(X509NameType.SimpleName, false);

            return Ok(new CertInfo { Name = certCommonName, Thumbprint = cert.Thumbprint });
        }

        //[Authorize(Roles = "Admin")]
        //[HttpPost]
        //public IActionResult RevokeCert([FromForm] FileModel model, CancellationToken cancelToken) {
        //    var result = GetCertInfo(model, cancelToken);

        //    if (result.StatusCode != (int)HttpStatusCode.OK || result.Value is null) {
        //        return result;
        //    }

        //    var value = (CertInfo)result.Value;
        //    return RevokeCert(value.Thumbprint, value.Name, cancelToken);
        //}

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public IActionResult CancelCertRevocation([FromBody] CertInfo cert) {
            var revokedCerts = _authService.CancelCertificateRevocation(cert.Thumbprint);
            if (revokedCerts is null) {
                var problemDetails = new ProblemDetails {
                    Status = (int)HttpStatusCode.InternalServerError,
                    Instance = null,
                    Title = "Could not cancel certificate revocation.",
                };
                return StatusCode(problemDetails.Status.Value, problemDetails);
            }
            var revokedList = revokedCerts
                .Select(rv => new CertInfo { Name = rv.Value?.ToString(), Thumbprint = rv.Key })
                .OrderBy(ci => ci.Name);
            return Ok(revokedList);
        }

        #endregion

        #region Agent Events to Manager (SPA)

        //see https://stackoverflow.com/questions/36227565/aspnet-core-server-sent-events-response-flush
        //also, maybe better: https://github.com/tpeczek/Lib.AspNetCore.ServerSentEvents and https://github.com/tpeczek/Demo.AspNetCore.ServerSentEvents

        async Task<IActionResult> GetAgentStateEventStream(CancellationToken cancelToken) {
            var resp = SetupSSEResponse();

            var jsonSerializerOptions = _jsonOptions.Value.JsonSerializerOptions;

            var changes = _agentProxyManager.GetAgentStateChanges();
            try {
                await foreach (var change in changes.WithCancellation(cancelToken).ConfigureAwait(false)) {
                    var statusJson = JsonSerializer.Serialize(change, jsonSerializerOptions);
                    await resp.WriteAsync($"data:{statusJson}\n\n", cancelToken).ConfigureAwait(false);
                    await resp.Body.FlushAsync(cancelToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) {
                _logger.LogInformation("{user}: Disconnected from agent state updates.", UserInfo());
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

        AgentProxy? TryGetAgentProxy(string agentId, out IActionResult? proxyResult) {
            if (!_agentProxyManager.TryGetProxy(agentId, out var proxy)) {
                var pd = new ProblemDetails {
                    Status = (int)HttpStatusCode.NotFound,
                    Title = "Agent does not exist.",
                };
                proxyResult = StatusCode(pd.Status.Value, pd);
            }
            else {
                proxyResult = null;
            }
            return proxy;
        }

        IActionResult PostAgent(AgentProxy proxy, string eventName, string jsonData) {
            ProblemDetails pd;
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

        async Task<IActionResult> CallAgent(AgentProxy proxy, string eventName, string jsonData, TimeSpan timeout) {
            ProblemDetails pd;
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

        [HttpPost]
        public IActionResult Start(string agentId) {
            var proxy = TryGetAgentProxy(agentId, out var errorResult);
            if (proxy is null)
                return errorResult!;

            _logger.LogInformation("{user}: Starting agent {agentid}.", UserInfo(), agentId);
            return PostAgent(proxy, Constants.StartEvent, "");
        }

        [HttpPost]
        public IActionResult Stop(string agentId) {
            var proxy = TryGetAgentProxy(agentId, out var errorResult);
            if (proxy is null)
                return errorResult!;

            _logger.LogInformation("{user}: Stopping agent {agentid}.", UserInfo(), agentId);
            return PostAgent(proxy, Constants.StopEvent, "");
        }

        [HttpPost]
        public IActionResult GetState(string agentId) {
            var proxy = TryGetAgentProxy(agentId, out var errorResult);
            if (proxy is null)
                return errorResult!;
            return PostAgent(proxy, Constants.GetStateEvent, "");
        }

        [HttpPost]
        public IActionResult Reset(string agentId) {
            var proxy = TryGetAgentProxy(agentId, out var errorResult);
            if (proxy is null)
                return errorResult!;
            return PostAgent(proxy, Constants.ResetEvent, "");
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
            var proxy = TryGetAgentProxy(agentId, out var errorResult);
            if (proxy is null)
                return Task.FromResult(errorResult!);

            var dynamicParts = filterObj.GetProperty("dynamicParts").EnumerateArray().Select(dp => dp.GetString()).ToImmutableArray();
            var filter = dynamicParts.Length == 0
                ? new EtwLogging.Filter()  // we are clearing the filter
                : Filter.MergeFilterTemplate(dynamicParts); // WE are supplying the filter template

            var json = _jsonFormatter.Format(filter);
            _logger.LogInformation("{user}: Testing filter on agent {agentid}:\n{filter}", UserInfo(), agentId, json);
            return CallAgent(proxy, Constants.TestFilterEvent, json, TimeSpan.FromSeconds(15));
        }

        AgentOptions BuildAgentOptions(JsonElement rawOptions) {
            var result = new AgentOptions();

            var enabledProviders = rawOptions.GetProperty("enabledProviders");
            if (enabledProviders.ValueKind == JsonValueKind.Array) {
                result.HasEnabledProviders = true;
                foreach (var enabledProvider in enabledProviders.EnumerateArray()) {
                    if (enabledProvider.ValueKind != JsonValueKind.Object)
                        continue;
                    var provider = enabledProvider.GetRawText().ToProtoMessage<ProviderSetting>();
                    result.EnabledProviders.Add(provider);
                }
            }

            var dynamicFilterParts = rawOptions.GetProperty("dynamicFilterParts");
            if (dynamicFilterParts.ValueKind == JsonValueKind.Array) {
                var dynamicParts = dynamicFilterParts.EnumerateArray().Select(dp => dp.GetString()).ToImmutableArray();
                var filter = dynamicParts.Length == 0
                    ? new EtwLogging.Filter()  // we are clearing the filter
                    : Filter.MergeFilterTemplate(dynamicParts); // WE are supplying the filter template
                var processingOptions = new ProcessingOptions {
                    Filter = filter
                };
                result.ProcessingOptions = processingOptions;
            }

            var eventSinkProfiles = rawOptions.GetProperty("eventSinkProfiles");
            if (eventSinkProfiles.ValueKind == JsonValueKind.Array) {
                result.HasEventSinkProfiles = true;
                foreach (var eventSinkProfile in eventSinkProfiles.EnumerateArray()) {
                    if (eventSinkProfile.ValueKind != JsonValueKind.Object)
                        continue;
                    var profile = new EventSinkProfile {
                        Name = eventSinkProfile.GetProperty("name").GetString(),
                        SinkType = eventSinkProfile.GetProperty("sinkType").GetString(),
                        Version = eventSinkProfile.GetProperty("version").GetString(),
                        BatchSize = eventSinkProfile.GetProperty("batchSize").GetUInt32(),
                        MaxWriteDelayMSecs = eventSinkProfile.GetProperty("maxWriteDelayMSecs").GetUInt32(),
                        PersistentChannel = eventSinkProfile.GetProperty("persistentChannel").GetBoolean(),
                        Options = eventSinkProfile.GetProperty("options").ToString(),
                        Credentials = eventSinkProfile.GetProperty("credentials").ToString()
                    };
                    result.EventSinkProfiles.Add(profile.Name ?? "unknown", profile);
                }
            }

            var liveViewOptions = rawOptions.GetProperty("liveViewOptions");
            if (liveViewOptions.ValueKind == JsonValueKind.Object) {
                var lvOptions = liveViewOptions.GetRawText().ToProtoMessage<LiveViewOptions>();
                result.LiveViewOptions = lvOptions;
            }

            return result;
        }

        [HttpPost]
        public ContentResult GetAgentOptionsCommand(string agentId, [FromBody] JsonElement rawOptions) {
            var agentOptions = BuildAgentOptions(rawOptions);
            var agentOptionsJson = _jsonFormatter.Format(agentOptions);
            return Content(agentOptionsJson, new MediaTypeHeaderValue(MediaTypeNames.Application.Json));
        }

        [HttpPost]
        public Task<IActionResult> ApplyAgentOptions(string agentId, [FromBody] JsonElement rawOptions) {
            var proxy = TryGetAgentProxy(agentId, out var errorResult);
            if (proxy is null)
                return Task.FromResult(errorResult!);

            var agentOptions = BuildAgentOptions(rawOptions);

            var agentOptionsJson = _jsonFormatter.Format(agentOptions);
            _logger.LogInformation("{user}: Setting agent options on agent {agentid}:\n{options}", UserInfo(), agentId, agentOptionsJson);
            return CallAgent(proxy, Constants.ApplyAgentOptionsEvent, agentOptionsJson, TimeSpan.FromSeconds(15));
        }

        #endregion

        #region ETW Events to Manager (SPA)

        async Task<IActionResult> GetEtwEventStream(AgentProxy proxy, CancellationToken cancelToken) {
            var receivingSource = new TaskCompletionSource<int>();
            var etwEventStream = await proxy.GetEtwEventStream(receivingSource).ConfigureAwait(false);
            if (etwEventStream == null) {
                receivingSource.TrySetCanceled(cancelToken);
                return new EmptyResult();
            }

            int eventCount = 0;
            var jsonOptions = new JsonWriterOptions {
                Indented = false,
                SkipValidation = true,
            };
            try {
                var resp = SetupSSEResponse();
                var jsonWriter = new Utf8JsonWriter(resp.BodyWriter, jsonOptions);
                var dataPrefix = Encoding.UTF8.GetBytes("data:");
                var dataEnd = Encoding.UTF8.GetBytes("\n\n");

                while (await etwEventStream.MoveNext(cancelToken).ConfigureAwait(false)) {
                    var evtBatch = etwEventStream.Current;
                    eventCount += evtBatch.Events.Count;

                    resp.BodyWriter.Write(dataPrefix);
                    // write as canonical JSON
                    jsonWriter.Reset();
                    evtBatch.WriteJsonArray(jsonWriter);
                    jsonWriter.Flush();
                    resp.BodyWriter.Write(dataEnd);

                    await resp.Body.FlushAsync(cancelToken).ConfigureAwait(false);
                }
                receivingSource.TrySetResult(eventCount);
            }
            catch (Exception ex) {
                receivingSource.TrySetException(ex);
            }

            var evt = new ControlEvent {
                Id = proxy.GetNextEventId().ToString(),
                Event = Constants.StopLiveViewSinkEvent,
            };

            if (!proxy.Post(evt)) {
                var pd = new ProblemDetails {
                    Status = (int)HttpStatusCode.InternalServerError,
                    Title = "Could not post message.",
                };
                return StatusCode(pd.Status.Value, pd);
            }

            _logger.LogInformation("{user}: Stopping live view on agent {agentid}.", UserInfo(), proxy.AgentId);

            // OkResult not right here, tries to set status code which is not allowed once the response has started
            return new EmptyResult();
        }

        [HttpGet]
        public async Task<IActionResult> GetEtwEvents(string agentId, CancellationToken cancelToken) {
            var proxy = _agentProxyManager.ActivateProxy(agentId);
            var jsonSerializerOptions = _jsonOptions.Value.JsonSerializerOptions;

            // get latest version of gRPCSink
            var grpcSinkVersion = typeof(gRPCSink).Assembly.GetName().Version?.ToString();

            // send control message to Agent, telling it to activate the proper event sink
            var gRPCOpts = new gRPCSinkOptions { Host = proxy.ManagerUri };

            string? certSubjectCN = null;
            if (!string.IsNullOrEmpty(proxy.ClientCertDN)) {
                var certDN = new X500DistinguishedName(proxy.ClientCertDN);
                var certCNData = new AsnEncodedData("CN", certDN.RawData);
                certSubjectCN = certCNData.Format(false);
            }
            var gRPCCreds = new gRPCSinkCredentials {
                CertificateThumbPrint = proxy.ClientCertThumbprint,
                CertificateSubjectCN = certSubjectCN
            };
            var managerSinkProfile = new EventSinkProfile {
                BatchSize = 100,
                MaxWriteDelayMSecs = 400,
                Name = Constants.LiveViewSinkName,
                SinkType = nameof(gRPCSink),
                Version = grpcSinkVersion!,
                Options = JsonSerializer.Serialize(gRPCOpts, jsonSerializerOptions),
                Credentials = JsonSerializer.Serialize(gRPCCreds, jsonSerializerOptions),
                PersistentChannel = false,
            };

            var evt = new ControlEvent {
                Id = proxy.GetNextEventId().ToString(),
                Event = Constants.StartLiveViewSinkEvent,
                Data = _jsonFormatter.Format(managerSinkProfile),
            };

            if (!proxy.Post(evt)) {
                var pd = new ProblemDetails {
                    Status = (int)HttpStatusCode.InternalServerError,
                    Title = "Could not post message.",
                };
                return StatusCode(pd.Status.Value, pd);
            }

            _logger.LogInformation("{user}: Starting live view on agent {agentid}.", UserInfo(), agentId);

            if (Request.Headers[HeaderNames.Accept].Contains(Constants.EventStreamHeaderValue)) {
                return await GetEtwEventStream(proxy, cancelToken).ConfigureAwait(false);
            }

            return BadRequest();
        }

        #endregion
    }
}
