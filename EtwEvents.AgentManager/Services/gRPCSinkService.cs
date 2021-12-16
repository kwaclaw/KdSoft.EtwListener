using System.Threading.Tasks;
using Grpc.Core;
using KdSoft.EtwLogging;

namespace KdSoft.EtwEvents.AgentManager.Services
{
    public class gRPCSinkService: EtwSink.EtwSinkBase
    {
        public override Task<EtwEventResponse> SendEvents(IAsyncStreamReader<EtwEventBatch> requestStream, ServerCallContext context) {
            return base.SendEvents(requestStream, context);
        }
    }
}
