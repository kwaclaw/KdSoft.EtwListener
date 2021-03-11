using System;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using KdSoft.EtwLogging;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Extensions.Logging;

namespace KdSoft.EtwEvents.Server
{
    class EtwListenerService: EtwListener.EtwListenerBase
    {
        readonly TraceSessionManager _sesManager;
        readonly ILoggerFactory _loggerFactory;
        readonly ILogger<EtwListenerService> _logger;
        static readonly Task<Empty> _emptyTask = Task.FromResult(new Empty());

        public EtwListenerService(TraceSessionManager sesManager, ILoggerFactory loggerFactory) {
            this._sesManager = sesManager;
            this._loggerFactory = loggerFactory;
            this._logger = loggerFactory.CreateLogger<EtwListenerService>();
        }

        RealTimeTraceSession GetSession(string name) {
            if (_sesManager.TryGetValue(name, out var session)) {
                session.GetLifeCycle().Used();
                return session;
            }
            throw new RpcException(new Status(StatusCode.NotFound, "Session"), "Session not found.");
        }

        public override Task<EnableProvidersResult> OpenSession(OpenEtwSession request, ServerCallContext context) {
            var result = new EnableProvidersResult();
            var session = _sesManager.GetOrAdd(
                request.Name,
                name => {
                    var logger = _loggerFactory.CreateLogger<RealTimeTraceSession>();
                    return new RealTimeTraceSession(name, request.LifeTime.ToTimeSpan(), logger, request.TryAttach);
                }
            );
            session.GetLifeCycle().Used();

            if (session.IsCreated) {  // can only enable providers when new session was created
                foreach (var setting in request.ProviderSettings) {
                    var restarted = session.EnableProvider(setting);
                    result.Results.Add(new ProviderSettingResult { Name = setting.Name, Restarted = restarted });
                }
            }

            return Task.FromResult(result);
        }

        public override Task<Empty> CloseSession(CloseEtwSession request, ServerCallContext context) {
            var session = GetSession(request.Name);
            session.StopEvents();
            session.GetLifeCycle().Terminate();
            return _emptyTask;
        }

        public override Task<EnableProvidersResult> EnableProviders(EnableProvidersRequest request, ServerCallContext context) {
            var result = new EnableProvidersResult();
            var session = GetSession(request.SessionName);
            foreach (var setting in request.ProviderSettings) {
                var restarted = session.EnableProvider(setting);
                result.Results.Add(new ProviderSettingResult { Name = setting.Name, Restarted = restarted });
            }
            return Task.FromResult(result);
        }

        public override Task<Empty> DisableProviders(DisableProvidersRequest request, ServerCallContext context) {
            var session = GetSession(request.SessionName);
            foreach (var provider in request.ProviderNames) {
                session.DisableProvider(provider);
            }
            return _emptyTask;
        }

        public override Task<SessionNamesResult> GetActiveSessionNames(Empty request, ServerCallContext context) {
            var result = new SessionNamesResult();
            result.SessionNames.AddRange(TraceEventSession.GetActiveSessionNames());
            return Task.FromResult(result);
        }

        public override Task<EtwSession> GetSession(StringValue request, ServerCallContext context) {
            var session = GetSession(request.Value);
            var result = new EtwSession {
                SessionName = request.Value,
                IsCreated = session.IsCreated,
                IsStarted = session.IsStarted,
                IsStopped = session.IsStopped
            };
            result.EnabledProviders.AddRange(session.EnabledProviders);

            return Task.FromResult(result);
        }

        async Task WakeUpClient(IServerStreamWriter<EtwEventBatch> responseStream, ServerCallContext context) {
            await context.WriteResponseHeadersAsync(Metadata.Empty).ConfigureAwait(false);
            var batch = new EtwEventBatch();
            await responseStream.WriteAsync(batch).ConfigureAwait(false);
        }

        public override async Task GetEvents(EtwEventRequest request, IServerStreamWriter<EtwEventBatch> responseStream, ServerCallContext context) {
            var logger = _loggerFactory.CreateLogger<EventQueue>();
            var eventQueue = new EventQueue(responseStream, context, logger, request.BatchSize);
            var session = GetSession(request.SessionName);
            try {
                // not strictly necessary, but helps "waking" up the receiving end by sending an initial message
                await WakeUpClient(responseStream, context).ConfigureAwait(false);
                await eventQueue.Process(session, request.MaxWriteDelay.ToTimeSpan()).ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                // ignore closing of connection
            }
        }

        public override Task<Empty> StopEvents(StringValue request, ServerCallContext context) {
            var session = GetSession(request.Value);
            session.StopEvents();
            return _emptyTask;
        }

        public override Task<BuildFilterResult> SetCSharpFilter(SetFilterRequest request, ServerCallContext context) {
            var session = GetSession(request.SessionName);
            var diagnostics = session.SetFilter(request.CsharpFilter);
            var result = new BuildFilterResult().AddDiagnostics(diagnostics);
            return Task.FromResult(result);
        }

        public override Task<BuildFilterResult> TestCSharpFilter(TestFilterRequest request, ServerCallContext context) {
            var diagnostics = RealTimeTraceSession.TestFilter(request.CsharpFilter);
            var result = new BuildFilterResult().AddDiagnostics(diagnostics);
            return Task.FromResult(result);
        }
    }
}
