using System;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using KdSoft.EtwLogging;
using tracing = Microsoft.Diagnostics.Tracing;

namespace EtwEvents.Server
{
    public class EtwListenerService: EtwListener.EtwListenerBase
    {
        readonly TraceSessionManager sesManager;
        readonly Empty empty = new Empty();

        public EtwListenerService(TraceSessionManager sesManager) {
            this.sesManager = sesManager;
        }

        TraceSession GetSession(string name) {
            if (sesManager.TryGetValue(name, out var session)) {
                session.GetLifeCycle().Used();
                return session;
            }
            throw new InvalidOperationException("Session not found.");
        }

        public override Task<EnableProvidersResult> OpenSession(OpenEtwSession request, ServerCallContext context) {
            var result = new EnableProvidersResult();
            var session = sesManager.GetOrAdd(request.Name, n => new TraceSession(n, request.LifeTime.ToTimeSpan(), request.TryAttach));
            session.GetLifeCycle().Used();

            if (session.IsCreated) {  // can only enable providers when new session was created
                foreach (var setting in request.ProviderSettings) {
                    var success = session.Instance.EnableProvider(setting.Name, (Microsoft.Diagnostics.Tracing.TraceEventLevel)setting.Level, setting.MatchKeywords);
                    result.Results.Add(new ProviderSettingResult { Name = setting.Name, Success = success });
                }
            }

            return Task.FromResult(result);
        }

        public override Task<Empty> CloseSession(CloseEtwSession request, ServerCallContext context) {
            var session = GetSession(request.Name);
            session.StopEvents();
            session.GetLifeCycle().Terminate();
            return Task.FromResult(empty);
        }

        public override Task<EnableProvidersResult> EnableProviders(EnableProvidersRequest request, ServerCallContext context) {
            var result = new EnableProvidersResult();
            var session = GetSession(request.SessionName);
            foreach (var setting in request.ProviderSettings) {
                var success = session.Instance.EnableProvider(setting.Name, (Microsoft.Diagnostics.Tracing.TraceEventLevel)setting.Level, setting.MatchKeywords);
                result.Results.Add(new ProviderSettingResult { Name = setting.Name, Success = success });
            }
            return Task.FromResult(result);
        }

        public override Task<Empty> DisableProviders(DisableProvidersRequest request, ServerCallContext context) {
            var session = GetSession(request.SessionName);
            foreach (var provider in request.ProviderNames) {
                session.Instance.DisableProvider(provider);
            }
            return Task.FromResult(empty);
        }

        //TODO handle that we can call GetEvents only once on real-time sessions, maybe we have to re-attach or close/open?

        public override async Task GetEvents(EtwEventRequest request, IServerStreamWriter<EtwEvent> responseStream, ServerCallContext context) {
            Task postEvent(tracing.TraceEvent evt) {
                if (context.CancellationToken.IsCancellationRequested)
                    return Task.CompletedTask;
                else
                    try {
                        return responseStream.WriteAsync(new EtwEvent(evt));
                    }
                    // sometimes a WriteAsync is underway when IsCancellationRequested becomes true
                    catch (InvalidOperationException) {
                        return Task.CompletedTask;
                    }
            }
            responseStream.WriteOptions = new WriteOptions(WriteFlags.NoCompress);

            var tcs = new TaskCompletionSource<object>();
            var session = GetSession(request.SessionName);
            bool started = session.StartEvents(postEvent, tcs, context.CancellationToken);

            await tcs.Task;
        }

        public override Task<Empty> SetCSharpFilter(SetFilterRequest request, ServerCallContext context) {
            var session = GetSession(request.SessionName);
            session.SetFilter(request.CsharpFilter);
            return Task.FromResult(empty);
        }
    }
}
