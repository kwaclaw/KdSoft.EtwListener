using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using KdSoft.EtwLogging;
using Microsoft.CodeAnalysis.Text;
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
            var logger = _loggerFactory.CreateLogger<ChannelEventProcessor>();

            var eventQueue = new ChannelEventStreamProcessor(responseStream, logger, request.BatchSize, (int)request.MaxWriteDelay.ToTimeSpan().TotalMilliseconds);
            var session = GetSession(request.SessionName);
            try {
                // not strictly necessary, but helps "waking" up the receiving end by sending an initial message
                await WakeUpClient(responseStream, context).ConfigureAwait(false);
                await eventQueue.Process(session, context.CancellationToken).ConfigureAwait(false);
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

        static (SourceText source, IReadOnlyList<TextChangeRange> partRanges) BuildSource(string filterTemplate, IList<string> filterParts) {
            int indx = 0;
            var markers = filterParts.Select(p => $"\u001D{indx++}").ToArray();
            var initSource = string.Format(CultureInfo.InvariantCulture, filterTemplate, markers);
            var initSourceText = SourceText.From(initSource);
            var partChanges = new TextChange[4];
            for (indx = 0; indx < 4; indx++) {
                var part = filterParts[indx] ?? String.Empty;
                int insertionIndex = initSource.IndexOf(markers[indx], StringComparison.Ordinal);
                partChanges[indx] = new TextChange(new TextSpan(insertionIndex, 2), part);
            }
            var changedSourceText = initSourceText.WithChanges(partChanges);
            var ranges = changedSourceText.GetChangeRanges(initSourceText);
            return (changedSourceText, ranges);
        }

        public static (SourceText? source, IReadOnlyList<TextChangeRange>? partRanges) BuildFilterSource(string filterTemplate, IList<string> filterParts) {
            (SourceText? source, IReadOnlyList<TextChangeRange>? partRanges) result;
            if (filterParts == null || filterParts.Count == 0 || string.IsNullOrWhiteSpace(filterParts[filterParts.Count - 1]))
                result = (null, null);
            else if (string.IsNullOrWhiteSpace(filterTemplate))
                result = (null, null);
            else
                result = BuildSource(filterTemplate, filterParts);
            return result;
        }


        public override Task<BuildFilterResult> SetCSharpFilter(SetFilterRequest request, ServerCallContext context) {
            var result = new BuildFilterResult();

            var filterSource = BuildFilterSource(request.FilterTemplate, request.FilterParts);
            if (filterSource.source == null) {
                result.FilterSource = null;
            }
            else {
                var diagnostics = RealTimeTraceSession.TestFilter(filterSource.source);
                result.AddDiagnostics(diagnostics);
                result.FilterSource.AddSourceLines(filterSource.source.Lines);
            }

            return Task.FromResult(result);
        }

        public override Task<BuildFilterResult> TestCSharpFilter(TestFilterRequest request, ServerCallContext context) {
            var result = new BuildFilterResult();

            var filterSource = BuildFilterSource(request.FilterTemplate, request.FilterParts);
            if (filterSource.source == null) {
                result.FilterSource = null;
            }
            else {
                var diagnostics = RealTimeTraceSession.TestFilter(filterSource.source);
                result.AddDiagnostics(diagnostics);
                result.FilterSource.AddSourceLines(filterSource.source.Lines);
            }

            return Task.FromResult(result);
        }
    }
}
