using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace KdSoft.EtwEvents.Server
{
    public class AuthInterceptor: Interceptor
    {
        readonly ISet<string> _authorizedNames;

        public AuthInterceptor(ISet<string> authorizedNames) {
            this._authorizedNames = authorizedNames;
        }

        void CheckAuthorized(ServerCallContext context) {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (context.AuthContext.IsPeerAuthenticated) {
                foreach (var peer in context.AuthContext.PeerIdentity) {
                    if (_authorizedNames.Contains(peer.Value))
                        return;
                }
            }
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Unauthorized Certificate"));
        }

        public override Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(IAsyncStreamReader<TRequest> requestStream, ServerCallContext context, ClientStreamingServerMethod<TRequest, TResponse> continuation) {
            CheckAuthorized(context);
            return base.ClientStreamingServerHandler(requestStream, context, continuation);
        }

        public override Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation) {
            CheckAuthorized(context);
            return continuation.Invoke(request, context);
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
