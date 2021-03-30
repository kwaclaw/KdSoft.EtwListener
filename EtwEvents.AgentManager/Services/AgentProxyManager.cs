using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using KdSoft.EtwEvents.PushAgent;
using KdSoft.EtwEvents.PushAgent.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace KdSoft.EtwEvents.AgentManager.Services
{
    public class AgentProxyManager
    {
        readonly int _keepAliveMSecs;
        readonly ConcurrentDictionary<string, AgentProxy> _proxies;
        readonly AggregatingNotifier<AgentStates> _changeNotifier;
        readonly Timer _keepAliveTimer;
        readonly ILogger<AgentProxy> _logger;

        public static ControlEvent KeepAliveMessage = new ControlEvent { Event = AgentProxy.KeepAliveEvent };
        public static ControlEvent CloseMessage = new ControlEvent { Event = AgentProxy.CloseEvent };
        public static ControlEvent GetStateMessage = new ControlEvent { Event = AgentProxy.GetStateEvent };

        public AgentProxyManager(IConfiguration config, ILogger<AgentProxy> logger) {
            var keepAlivePeriod = TimeSpan.TryParse(config?["ControlChannel:KeepAlivePeriod"], out var reapPeriod) ? reapPeriod : TimeSpan.FromSeconds(20);
            this._keepAliveMSecs = (int)keepAlivePeriod.TotalMilliseconds;
            this._keepAliveTimer = new Timer(KeepAlive, this, keepAlivePeriod, keepAlivePeriod);
            this._logger = logger;
            _proxies = new ConcurrentDictionary<string, AgentProxy>();
            this._changeNotifier = new AggregatingNotifier<AgentStates>(GetAgentStates);
        }

        public void Dispose() {
            foreach (var entry in _proxies) {
                entry.Value.Writer.TryComplete();
            }
        }

        public AgentProxy ActivateProxy(string agentId) {
            var result = _proxies.GetOrAdd(agentId, key => {
                var proxy = new AgentProxy(agentId, _logger);
                proxy.Completion.ContinueWith((tsk) => {
                    _proxies.TryRemove(key, out var _);
                });
                proxy.Used();
                return proxy;
            });
            return result;
        }

        public bool TryGetProxy(string agentId, [MaybeNullWhen(false)] out AgentProxy proxy) {
            return _proxies.TryGetValue(agentId, out proxy);
        }

        public void KeepAlive(object? state) {
            foreach (var entry in _proxies) {
                var agentProxy = entry.Value;
                // integer subtraction is immune to rollover, e.g. unchecked(int.MaxValue + y) - (int.MaxValue - x) = y + x;
                // Environment.TickCount rolls over from int.Maxvalue to int.MinValue!
                var deltaMSecs = Environment.TickCount - agentProxy.TimeStamp;
                if (deltaMSecs >= _keepAliveMSecs) {
                    //agentProxy.Writer.TryWrite(KeepAliveMessage);
                    // we can use GetState as keep alive message as long as the data size is not too big
                    agentProxy.Writer.TryWrite(GetStateMessage);
                }
            }
        }

        public Task<AgentStates> GetAgentStates() {
            var asb = ImmutableArray.CreateBuilder<AgentState>();
            foreach (var entry in _proxies) {
                if (!entry.Value.IsConnected())
                    continue;
                var agentState = entry.Value.GetState();
                if (agentState != null)
                    asb.Add(agentState);
            }
            var result = new AgentStates { Agents = asb.ToImmutableArray() };
            return Task.FromResult(result);
        }

        public IAsyncEnumerable<AgentStates> GetAgentStateChanges() {
            return _changeNotifier.GetNotifications();
        }

        public ValueTask PostAgentStateChange() {
            return _changeNotifier.PostNotification();
        }
    }
}
