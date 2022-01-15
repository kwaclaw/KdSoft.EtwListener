using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LaunchDarkly.EventSource;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KdSoft.EtwEvents.PushAgent
{
    class ControlConnector: IDisposable
    {
        readonly SocketsHttpHandler _httpHandler;
        readonly ILogger<ControlConnector> _logger;
        readonly TaskCompletionSource _tcs;
        readonly object _syncObj = new object();
        
        EventSource? _eventSource;
        CancellationTokenRegistration _cancelRegistration;

        public ControlConnector(
            SocketsHttpHandler httpHandler,
            IOptionsMonitor<ControlOptions> controlOptions,
            ILogger<ControlConnector> logger
         ) {
            this._httpHandler = httpHandler;
            this._logger = logger;
            _tcs = new TaskCompletionSource();
        }

        public Task RunTask => _tcs.Task;

        void CloseEventSource(EventSource evt, Task sseTask) {
            sseTask.ContinueWith(st => {
                if (st.IsFaulted && st.Exception != null) {
                    _tcs.TrySetException(st.Exception);
                }
                else if (st.IsCanceled) {
                    _tcs.TrySetCanceled();
                }
                else {
                    _tcs.TrySetResult();
                }

            });
            evt.Close();
        }

        public ControlOptions? CurrentOptions { get; private set; }

        public Task Start(ControlOptions opts, Func<ControlEvent, Task> processEvent, CancellationToken stoppingToken) {
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

            lock (_syncObj) {
                var oldEventSource = _eventSource;
                if (oldEventSource != null) {
                    _cancelRegistration.Dispose();
                    oldEventSource.Close();
                    oldEventSource.Dispose();
                }

                evt.MessageReceived += (s, e) => EventReceived(e, processEvent);
                evt.Error += EventError;
                evt.Opened += EventSourceStateChanged;
                evt.Closed += EventSourceStateChanged;

                CancellationTokenRegistration registration = default;
                try {
                    var sseTask = evt.StartAsync();
                    registration = stoppingToken.Register(() => CloseEventSource(evt, sseTask));
                    _cancelRegistration = registration;
                    _eventSource = evt;
                    CurrentOptions = opts;
                }
                catch (Exception ex) {
                    registration.Dispose();
                    evt.Dispose();
                    _cancelRegistration = default;
                    _eventSource = null;
                    _logger?.LogError(ex, "Error starting EventSource.");
                }
            }

            // this Task will only terminate when the last sseTask terminates!
            return _tcs.Task;
        }

        async void EventReceived(MessageReceivedEventArgs e, Func<ControlEvent, Task> processEvent) {
            try {
                var lastEventIdStr = string.IsNullOrEmpty(e.Message.LastEventId) ? "-1" : e.Message.LastEventId;
                var messageDataStr = string.IsNullOrEmpty(e.Message.Data) ? "<None>" : e.Message.Data;
                _logger?.LogInformation("{method}: {eventName}-{lastEventId}, {messageData}", nameof(EventReceived), e.EventName, lastEventIdStr, messageDataStr);
                await processEvent(new ControlEvent { Event = e.EventName, Id = e.Message.LastEventId ?? "", Data = e.Message.Data ?? "" }).ConfigureAwait(false);
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
                lock (_syncObj) {
                    var evt = _eventSource;
                    if (evt != null) {
                        evt.Dispose();
                    }
                }
            }
            finally {
                // just in case
                _tcs.TrySetResult();
            }
        }
    }
}
