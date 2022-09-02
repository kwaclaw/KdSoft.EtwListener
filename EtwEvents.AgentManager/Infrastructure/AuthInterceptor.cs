using System;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Interceptors;
using KdSoft.EtwEvents.AgentManager;

namespace KdSoft.EtwEvents
{
    public class AuthInterceptor: Interceptor
    {
        readonly AuthorizationService _authService;

        public AuthInterceptor(AuthorizationService authService) {
            this._authService = authService;
        }

        void CheckAuthorized(ServerCallContext context) {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var authorized = _authService.AuthorizePeerIdentity(context.AuthContext.PeerIdentity, context, Role.Agent);
            if (authorized) {
                return;
            }

            throw new RpcException(new Status(StatusCode.PermissionDenied, "Unauthorized or missing Certificate"));
        }

        public override Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation) {
            CheckAuthorized(context);
            return continuation(request, context);
        }

        public override Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(IAsyncStreamReader<TRequest> requestStream, ServerCallContext context, ClientStreamingServerMethod<TRequest, TResponse> continuation) {
            CheckAuthorized(context);
            return base.ClientStreamingServerHandler(requestStream, context, continuation);
        }

        public override Task ServerStreamingServerHandler<TRequest, TResponse>(TRequest request, IServerStreamWriter<TResponse> responseStream, ServerCallContext context, ServerStreamingServerMethod<TRequest, TResponse> continuation) {
            CheckAuthorized(context);
            return base.ServerStreamingServerHandler(request, responseStream, context, continuation);
        }

        public override Task DuplexStreamingServerHandler<TRequest, TResponse>(IAsyncStreamReader<TRequest> requestStream, IServerStreamWriter<TResponse> responseStream, ServerCallContext context, DuplexStreamingServerMethod<TRequest, TResponse> continuation) {
            CheckAuthorized(context);
            return base.DuplexStreamingServerHandler(requestStream, responseStream, context, continuation);
        }
    }
}
