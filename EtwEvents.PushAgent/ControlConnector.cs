using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using LaunchDarkly.EventSource;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KdSoft.EtwEvents.PushAgent
{
    class ControlConnector: IDisposable
    {
        public static readonly ControlEvent GetStateMessage = new ControlEvent { Event = Constants.GetStateEvent };

        readonly SocketsHttpHandler _httpHandler;
        readonly Channel<ControlEvent> _channel;
        readonly ILogger<ControlConnector> _logger;
        readonly TaskCompletionSource _tcs;

        ControlContext? _controlContext;

        public ControlConnector(
            SocketsHttpHandler httpHandler,
            Channel<ControlEvent> channel,
            IOptionsMonitor<ControlOptions> controlOptions,
            ILogger<ControlConnector> logger
         ) {
            this._httpHandler = httpHandler;
            this._channel = channel;
            this._logger = logger;
            _tcs = new TaskCompletionSource();
        }

        // this Task will only terminate when the last sseTask terminates!
        public Task RunTask => _tcs.Task;

        public ControlOptions? CurrentOptions { get; private set; }

        EventSource ConfigureEventSource(ControlOptions opts) {
            var evtUri = new Uri(opts.Uri, "Agent/GetEvents");
            var cfgBuilder = Configuration.Builder(evtUri).HttpMessageHandler(_httpHandler);
            if (opts.InitialRetryDelay != null)
                cfgBuilder.InitialRetryDelay(opts.InitialRetryDelay.Value);
            if (opts.MaxRetryDelay != null)
                cfgBuilder.MaxRetryDelay(opts.MaxRetryDelay.Value);
            if (opts.BackoffResetThreshold != null)
                cfgBuilder.BackoffResetThreshold(opts.BackoffResetThreshold.Value);
            var config = cfgBuilder.Build();

            var evt = new EventSource(config);
            evt.MessageReceived += (s, e) => EventReceived(e);
            evt.Error += EventError;
            evt.Opened += EventSourceStateChanged;
            evt.Closed += EventSourceStateChanged;

            return evt;
        }

        /// <summary>
        /// Starts or restarts the SSE connection.
        /// </summary>
        /// <param name="opts">Options for the EventSource.</param>
        /// <param name="stoppingToken">CancellationToken to stop the connector for good.</param>
        public async Task<bool> StartAsync(ControlOptions opts, CancellationToken stoppingToken) {
            var evt = ConfigureEventSource(opts);
            var controlContext = new ControlContext(evt, _logger);

            var oldContext = Interlocked.Exchange(ref _controlContext, controlContext);
            if (oldContext != null) {
                await oldContext.StopAsync().ConfigureAwait(false);
            }

            var registration = stoppingToken.Register(async () => await controlContext.StopAsync(_tcs));
            if (!controlContext.Start(registration)) {
                registration.Dispose();
                return false;
            }
            return true;
        }

        void EventReceived(MessageReceivedEventArgs e) {
            try {
                var lastEventIdStr = string.IsNullOrEmpty(e.Message.LastEventId) ? "-1" : e.Message.LastEventId;
                var messageDataStr = string.IsNullOrEmpty(e.Message.Data) ? "<None>" : e.Message.Data;
                if (e.EventName == Constants.GetStateEvent) {
                    _logger?.LogDebug("{method}: {eventName}-{lastEventId}, {messageData}", nameof(EventReceived), e.EventName, lastEventIdStr, messageDataStr);
                }
                else {
                    _logger?.LogInformation("{method}: {eventName}-{lastEventId}, {messageData}", nameof(EventReceived), e.EventName, lastEventIdStr, messageDataStr);
                }

                if (e.EventName == Constants.CloseEvent) {
                    _channel.Writer.TryComplete();
                }
                else {
                    var controlEvent = new ControlEvent { Event = e.EventName, Id = e.Message.LastEventId ?? "", Data = e.Message.Data ?? "" };
                    var couldWrite = _channel.Writer.TryWrite(controlEvent);
                    if (!couldWrite) {
                        _logger?.LogError("Error in {method}. Could not write event {event} to control channel, event data:\n{data}",
                            nameof(EventReceived), controlEvent.Event, controlEvent.Data);
                    }
                }
            }
            catch (Exception ex) {
                _logger?.LogError(ex, "Error in {method}.", nameof(EventReceived));
            }
        }

        void EventError(object? sender, ExceptionEventArgs e) {
            _logger?.LogError(e.Exception, "Error in EventSource.");
        }

        void EventSourceStateChanged(object? sender, StateChangedEventArgs e) {
            _logger?.LogInformation("{method}: {readyState}", nameof(EventSourceStateChanged), e.ReadyState);
        }

        public void Dispose() {
            try {
                var context = Interlocked.Exchange(ref _controlContext, null);
                if (context != null) {
                    context.Source.Dispose();
                    context.CancelRegistration.Dispose();
                }
            }
            finally {
                // just in case
                _tcs.TrySetResult();
            }
        }
    }
}
