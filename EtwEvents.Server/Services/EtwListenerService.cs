using System;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using KdSoft.EtwLogging;
using tracing = Microsoft.Diagnostics.Tracing;

namespace EtwEvents.Server
{
    class EtwListenerService: EtwListener.EtwListenerBase
    {
        readonly TraceSessionManager sesManager;
        static readonly Task<Empty> emptyTask = Task.FromResult(new Empty());

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
                    var restarted = session.Instance.EnableProvider(setting.Name, (tracing.TraceEventLevel)setting.Level, setting.MatchKeywords);
                    result.Results.Add(new ProviderSettingResult { Name = setting.Name, Restarted = restarted });
                }
            }

            return Task.FromResult(result);
        }

        public override Task<Empty> CloseSession(CloseEtwSession request, ServerCallContext context) {
            var session = GetSession(request.Name);
            session.StopEvents();
            session.GetLifeCycle().Terminate();
            return emptyTask;
        }

        public override Task<EnableProvidersResult> EnableProviders(EnableProvidersRequest request, ServerCallContext context) {
            var result = new EnableProvidersResult();
            var session = GetSession(request.SessionName);
            foreach (var setting in request.ProviderSettings) {
                var restarted = session.Instance.EnableProvider(setting.Name, (tracing.TraceEventLevel)setting.Level, setting.MatchKeywords);
                result.Results.Add(new ProviderSettingResult { Name = setting.Name, Restarted = restarted });
            }
            return Task.FromResult(result);
        }

        public override Task<Empty> DisableProviders(DisableProvidersRequest request, ServerCallContext context) {
            var session = GetSession(request.SessionName);
            foreach (var provider in request.ProviderNames) {
                session.Instance.DisableProvider(provider);
            }
            return emptyTask;
        }

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
            var realTimeSource = session.StartEvents(postEvent, tcs, context.CancellationToken);

            await tcs.Task;
        }

        public override Task<SetFilterResult> SetCSharpFilter(SetFilterRequest request, ServerCallContext context) {
            var session = GetSession(request.SessionName);
            var diagnostics = session.SetFilter(request.CsharpFilter);
            var result = new SetFilterResult();

            foreach (var diag in diagnostics) {
                LinePositionSpan lineSpan = null;
                if (diag.Location.IsInSource) {
                    var ls = diag.Location.GetLineSpan();
                    lineSpan = new LinePositionSpan {
                        Start = new LinePosition { Line = ls.StartLinePosition.Line, Character = ls.StartLinePosition.Character },
                        End = new LinePosition { Line = ls.EndLinePosition.Line, Character = ls.EndLinePosition.Character }
                    };
                }
                var dg = new CompileDiagnostic {
                    Id = diag.Id,
                    IsWarningAsError = diag.IsWarningAsError,
                    WarningLevel = diag.WarningLevel,
                    Severity = (CompileDiagnosticSeverity)diag.Severity,
                    Message = diag.GetMessage(),
                    LineSpan = lineSpan
                };
                result.Diagnostics.Add(dg);
            }
            return Task.FromResult(result);
        }
    }
}
