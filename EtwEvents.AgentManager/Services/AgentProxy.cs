using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using KdSoft.EtwEvents.PushAgent;
using KdSoft.EtwEvents.PushAgent.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace KdSoft.EtwEvents.AgentManager.Services
{
    public class AgentProxy
    {
        readonly Channel<ControlEvent> _channel;
        readonly ILogger _logger;
        readonly object _syncObj = new object();

        CancellationTokenSource? _connectionTokenSource;
        AgentState _state;
        int _connected;

        public AgentProxy(string agentId, Channel<ControlEvent> channel, ILogger logger) {
            this._channel = channel;
            this._logger = logger;
            this._state = new AgentState { Id = agentId };
        }

        public AgentProxy(string agentId, ILogger logger) : this(agentId, Channel.CreateUnbounded<ControlEvent>(), logger) {
            //
        }

        public string AgentId { get { return _state.Id; } }

        public ChannelWriter<ControlEvent> Writer => _channel.Writer;

        int _timeStamp;
        public int TimeStamp {
            get {
                Interlocked.MemoryBarrier();
                return _timeStamp;
            }
        }

        public Task Completion => _channel.Reader.Completion;

        public void SetState(AgentState state) {
            lock (_syncObj) {
                if (this._state.Id != state.Id)
                    throw new ArgumentException("Must not set agent state with different name", nameof(state));
                this._state = state;
            }
        }

        public AgentState GetState() {
            lock (_syncObj) {
                return _state;
            }
        }

        public bool IsConnected() {
            var connected = Volatile.Read(ref _connected);
            return connected != 0;
        }

        public void Used() {
            Interlocked.MemoryBarrier();
            _timeStamp = Environment.TickCount;
            Interlocked.MemoryBarrier();
        }

        void InitializeResponse(HttpResponse response) {
            response.ContentType = Constants.EventStreamHeaderValue;
            response.Headers[HeaderNames.CacheControl] = "no-cache";
            response.Headers[HeaderNames.Pragma] = "no-cache";
            //response.Headers[HeaderNames.Connection] = "keep-alive";
            response.Headers[HeaderNames.ContentEncoding] = "identity";

            // Make sure we disable all response buffering for SSE
            var bufferingFeature = response.HttpContext.Features.Get<IHttpResponseBodyFeature>();
            bufferingFeature?.DisableBuffering();
        }

        /// <summary>
        /// Starts processing queued messages/events for the given HttpResponse instance.
        /// Cancels the previously associated connection and response instance.
        /// </summary>
        /// <param name="response"><see cref="HttpResponse"/> instance to write to.</param>
        /// <param name="connectionToken"><see cref="CancellationToken"/> for new response/connection.</param>
        /// <returns><c>true</c> when finished normally, <c>false</c> when cancelled.</returns>
        public async Task<bool> ProcessMessages(HttpResponse response, CancellationToken connectionToken) {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(connectionToken);
            var oldCts = Interlocked.Exchange(ref _connectionTokenSource, cts);
            if (oldCts != null) {
                oldCts.Cancel();
                oldCts.Dispose();
            }
            var linkedToken = cts.Token;

            InitializeResponse(response);

            bool finished = true;
            try {
                Volatile.Write(ref _connected, 99);
                await foreach (var sse in _channel.Reader.ReadAllAsync(linkedToken).ConfigureAwait(false)) {
                    if (sse.Event == Constants.CloseEvent) {
                        Writer.TryComplete();
                    }
                    else {
                        // updated last used time stamp so we know when to send keep-alive messages
                        Used();
                    }

                    string msg = sse.Event == Constants.KeepAliveEvent ? ":\n\n" : $"event:{sse.Event}\nid:{sse.Id}\ndata:{sse.Data}\n\n";
                    await response.WriteAsync(msg, linkedToken).ConfigureAwait(false);
                    await response.Body.FlushAsync(linkedToken).ConfigureAwait(false);

                    _logger.LogInformation($"Pushed Control Event: {sse.Event}:{sse.Id} -> {AgentId}");

                    if (linkedToken.IsCancellationRequested) {
                        finished = false;
                        break;
                    }
                }
            }
            catch (OperationCanceledException) {
                finished = false;
            }
            finally {
                Volatile.Write(ref _connected, 0);
            }

            return finished;
        }
    }
}
