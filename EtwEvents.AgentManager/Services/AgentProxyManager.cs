using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using KdSoft.EtwEvents.PushAgent;
using Microsoft.Extensions.Logging;

namespace KdSoft.EtwEvents.AgentManager.Services
{
    public class AgentProxyManager
    {
        readonly int _keepAliveMSecs;
        readonly ConcurrentDictionary<string, AgentProxy> _proxies;
        readonly Timer _lifeCycleTimer;
        readonly ILogger<AgentProxy> _logger;

        public static ControlEvent KeepAliveMessage = new ControlEvent { Event = AgentProxy.KeepAliveEvent };
        public static ControlEvent CloseMessage = new ControlEvent { Event = AgentProxy.CloseEvent };

        public AgentProxyManager(TimeSpan keepAlivePeriod, ILogger<AgentProxy> logger) {
            this._keepAliveMSecs = (int)keepAlivePeriod.TotalMilliseconds;
            this._logger = logger;
            _proxies = new ConcurrentDictionary<string, AgentProxy>();
            _lifeCycleTimer = new Timer(KeepAlive, this, keepAlivePeriod, keepAlivePeriod);
        }

        public void Dispose() {
            _lifeCycleTimer?.Dispose();
            foreach (var entry in _proxies) {
                entry.Value.Writer.TryComplete();
            }
        }

        public AgentProxy ActivateProxy(string agentId) {
            var result = _proxies.GetOrAdd(agentId, key => {
                var queue = new AgentProxy(agentId, _logger);
                queue.Completion.ContinueWith((tsk) => {
                    _proxies.TryRemove(key, out var _);
                });
                queue.Used();
                return queue;
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
                    agentProxy.Writer.TryWrite(KeepAliveMessage);
                }
            }
        }
    }
}
