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

        public override Task GetEvents(EtwEventRequest request, IServerStreamWriter<EtwEvent> responseStream, ServerCallContext context) {
            var session = GetSession(request.SessionName);
            responseStream.WriteOptions = new WriteOptions(WriteFlags.NoCompress);

            var filter = session.GetFilter(); // this performs a lock
            session.Instance.Source.Dynamic.All += async (tracing.TraceEvent evt) => {
                if (TplActivities.TplEventSourceGuid.Equals(evt.ProviderGuid))
                    return;
                session.CheckFilterChanged(ref filter);
                if (filter != null && filter.IncludeEvent(evt))
                    await responseStream.WriteAsync(new EtwEvent(evt));
            };

            session.Instance.Source.Process();
            return Task.CompletedTask;
        }

        public override Task<Empty> SetCSharpFilter(SetFilterRequest request, ServerCallContext context) {
            var session = GetSession(request.SessionName);
            session.SetFilter(request.CsharpFilter);
            return Task.FromResult(empty);
        }
    }
}
