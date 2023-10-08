using Grpc.Core;
using KdSoft.EtwLogging;

namespace KdSoft.EtwEvents.AgentManager
{
    public class LiveViewSinkService: EtwSink.EtwSinkBase
    {
        readonly AgentProxyManager _agentProxyManager;
        readonly ILogger<LiveViewSinkService> _logger;

        static string GetAgentIdentity(ServerCallContext context) {
            var ids = context.AuthContext.PeerIdentity;
            if (ids != null) {
                foreach (var id in ids) {
                    if (string.Equals(id.Name, "x509_common_name", System.StringComparison.OrdinalIgnoreCase))
                        return id.Value;
                    else if (string.Equals(id.Name, "x509_subject_alternative_name", System.StringComparison.OrdinalIgnoreCase))
                        return id.Value;
                }
            }
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Unauthorized."));
        }

        public LiveViewSinkService(ILogger<LiveViewSinkService> logger, AgentProxyManager agentProxyManager) {
            this._agentProxyManager = agentProxyManager;
            this._logger = logger;
        }

        public override async Task<EtwEventResponse> SendEvents(IAsyncStreamReader<EtwEventBatch> requestStream, ServerCallContext context) {
            var agentId = GetAgentIdentity(context);
            if (!_agentProxyManager.TryGetProxy(agentId, out var proxy)) {
                throw new RpcException(new Status(StatusCode.NotFound, "Agent proxy not found."));
            }

            try {
                var eventCount = await proxy.ProcessEventStream(requestStream).ConfigureAwait(false);
                return new EtwEventResponse { EventsReceived = eventCount };
            }
            catch (Exception ex) {
                if (context.CancellationToken.IsCancellationRequested) {
                    _logger.LogDebug("Live View event stream closed for agent {agent}.", agentId);
                    return new EtwEventResponse { EventsReceived = -1 };
                }
                _logger.LogError(ex, "Error processing Live View event stream.");
                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }
    }
}
