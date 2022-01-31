﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Grpc.Core;
using KdSoft.EtwLogging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace KdSoft.EtwEvents.AgentManager
{
    public class AgentProxy
    {
        readonly Channel<ControlEvent> _channel;
        readonly ILogger _logger;
        readonly object _syncObj = new object();
        readonly Dictionary<string, TaskCompletionSource<string>> _pendingResponses;

        CancellationTokenSource? _connectionTokenSource;
        AgentState _state;
        int _connected;
        int _eventId;

        public AgentProxy(string agentId, Channel<ControlEvent> channel, ILogger logger) {
            this._channel = channel;
            this._logger = logger;
            this._state = new AgentState { Id = agentId };
            this._pendingResponses = new Dictionary<string, TaskCompletionSource<string>>();
        }

        public AgentProxy(string agentId, ILogger logger) : this(agentId, Channel.CreateUnbounded<ControlEvent>(), logger) {
            //
        }

        public string AgentId { get { return _state.Id; } }

        public int GetNextEventId() {
            return Interlocked.Increment(ref _eventId);
        }

        public bool Post(ControlEvent evt) {
            return _channel.Writer.TryWrite(evt);
        }

        public Task<string> CallAsync(string eventId, ControlEvent evt, CancellationToken cancelToken) {
            if (!_channel.Writer.TryWrite(evt))
                //TODO better Exception type
                throw new Exception("Could not post event.");

            var tcs = new TaskCompletionSource<string>();
            cancelToken.Register(() => {
                lock (_syncObj) {
                    if (_pendingResponses.Remove(eventId)) {
                        tcs.TrySetCanceled();
                    }
                }
            });

            lock (_syncObj) {
                _pendingResponses.Add(eventId, tcs);
                return tcs.Task;
            }
        }

        public bool TryComplete() {
            return _channel.Writer.TryComplete();
        }

        public bool CompleteResponse(string eventId, string responseJson) {
            TaskCompletionSource<string>? tcs;
            lock (_syncObj) {
                if (!_pendingResponses.Remove(eventId, out tcs))
                    return false;
                return tcs.TrySetResult(responseJson);
            }
        }

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
                        _channel.Writer.TryComplete();
                    }
                    else {
                        // updated last used time stamp so we know when to send keep-alive messages
                        Used();
                    }

                    string msg = sse.Event == Constants.KeepAliveEvent ? ":\n\n" : $"event:{sse.Event}\nid:{sse.Id}\ndata:{sse.Data}\n\n";
                    await response.WriteAsync(msg, linkedToken).ConfigureAwait(false);
                    await response.Body.FlushAsync(linkedToken).ConfigureAwait(false);

                    _logger.LogInformation("Pushed Control Event: {event}:{eventId} -> {agentId}", sse.Event, sse.Id, AgentId);

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

        TaskCompletionSource<IAsyncStreamReader<EtwEventBatch>?>? _eventStreamSource;
        Task<int>? _eventProcessingTask;

        int CompleteEventStream(Task<int> processingTask) {
            var oldSource = Interlocked.Exchange(ref _eventStreamSource, null);
            if (oldSource != null) {
                oldSource.TrySetCanceled();
            }
            return processingTask.Result;
        }

        public Task<IAsyncStreamReader<EtwEventBatch>?> GetEtwEventStream(TaskCompletionSource<int> processingSource) {
            var tcs = new TaskCompletionSource<IAsyncStreamReader<EtwEventBatch>?>();
            var oldSource = Interlocked.Exchange(ref _eventStreamSource, tcs);
            if (oldSource != null) {
                oldSource.TrySetCanceled();
            }

            var processingTask = processingSource.Task.ContinueWith(CompleteEventStream);
            var oldProcessingTask = Interlocked.Exchange(ref _eventProcessingTask, processingTask);

            return tcs.Task;
        }

        // returns -1 if unsuccessful
        public Task<int> ProcessEventStream(IAsyncStreamReader<EtwEventBatch> eventStream) {
            var evtStreamSource = _eventStreamSource;
            var evtProcTask = _eventProcessingTask;
            if (evtStreamSource != null) {
                if (!evtStreamSource.TrySetResult(eventStream))
                    return Task.FromResult(-1);
                if (evtProcTask != null) {
                    return evtProcTask;
                }
            }
            return Task.FromResult(-1);
        }

   }
}
