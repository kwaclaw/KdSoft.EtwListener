using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using KdSoft.EtwLogging;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Extensions.Logging;
using tracing = Microsoft.Diagnostics.Tracing;

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

        public override async Task GetEvents(EtwEventRequest request, IServerStreamWriter<EtwEvent> responseStream, ServerCallContext context) {
            // Need to queue events so that we can turn off WriteFlags.BufferHint when we want to flush
            int postCount = 0;
            var writeOptions = new WriteOptions(WriteFlags.NoCompress | WriteFlags.BufferHint);
            var flushWriteOptions = new WriteOptions(WriteFlags.NoCompress);
            EtwEvent emtpyEvent = new EtwEvent();

            Timer timer;

            Task postEvent(tracing.TraceEvent evt) {
                if (context.CancellationToken.IsCancellationRequested)
                    return Task.CompletedTask;
                if (postCount > 100) {
                    responseStream.WriteOptions = flushWriteOptions;
                    postCount = 0;
                }
                else {
                    postCount += 1;
                    responseStream.WriteOptions = writeOptions;
                }

                // change just before the call, so timer won't execute concurrently with this call
                timer.Change(300, Timeout.Infinite);

                return responseStream.WriteAsync(new EtwEvent(evt));
            }

            // we will flush buffered events if the last Write operation was too long ago
            async void timerCallback(object? state) {
                if (context.CancellationToken.IsCancellationRequested)
                    return;

                postCount = 0;
                responseStream.WriteOptions = flushWriteOptions;

                try {
                    // the empty event should be filtered out by the receiver
                    await responseStream.WriteAsync(emtpyEvent).ConfigureAwait(false);
                }
                // sometimes a WriteAsync is underway when IsCancellationRequested becomes true
                //catch (InvalidOperationException) {
                //    //
                //}
                catch (Exception ex) {
                    _logger.LogError(ex, $"Error in {nameof(timerCallback)}");
                }
            };

            var session = GetSession(request.SessionName);

            using (timer = new Timer(timerCallback)) {
                await session.StartEvents(postEvent, context.CancellationToken).ConfigureAwait(false);
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
            var result = new BuildFilterResult(diagnostics);
            return Task.FromResult(result);
        }

        public override Task<BuildFilterResult> TestCSharpFilter(TestFilterRequest request, ServerCallContext context) {
            var diagnostics = RealTimeTraceSession.TestFilter(request.CsharpFilter);
            var result = new BuildFilterResult(diagnostics);
            return Task.FromResult(result);
        }
    }
}
