using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using KdSoft.EtwLogging;

namespace KdSoft.EtwEvents.AgentManager
{
    public class AgentProxyManager: IDisposable
    {
        readonly int _keepAliveMSecs;
        readonly ConcurrentDictionary<string, AgentProxy> _proxies;
        readonly AggregatingNotifier<AgentStates> _changeNotifier;
        readonly Timer _keepAliveTimer;
        readonly ILogger<AgentProxy> _logger;

        public static readonly ControlEvent KeepAliveMessage = new() { Event = Constants.KeepAliveEvent };
        public static readonly ControlEvent CloseMessage = new() { Event = Constants.CloseEvent };
        public static readonly ControlEvent GetStateMessage = new() { Event = Constants.GetStateEvent };

        public AgentProxyManager(TimeSpan keepAlivePeriod, ILogger<AgentProxy> logger) {
            _keepAliveMSecs = (int)keepAlivePeriod.TotalMilliseconds;
            _keepAliveTimer = new Timer(KeepAlive, this, keepAlivePeriod, keepAlivePeriod);
            this._logger = logger;
            _proxies = new ConcurrentDictionary<string, AgentProxy>();
            _changeNotifier = new AggregatingNotifier<AgentStates>(GetAgentStates);
        }

        public AgentProxyManager(IConfiguration config, ILogger<AgentProxy> logger)
            : this(TimeSpan.TryParse(config?["ControlChannel:KeepAlivePeriod"], out var reapPeriod) ? reapPeriod : TimeSpan.FromSeconds(20), logger) {
            //
        }

        public void Dispose() {
            GC.SuppressFinalize(this);
            _keepAliveTimer.Dispose();
            foreach (var entry in _proxies) {
                entry.Value.TryComplete();
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
            int agentCount = 0;
            foreach (var entry in _proxies) {
                agentCount += 1;
                var agentProxy = entry.Value;
                // integer subtraction is immune to rollover, e.g. unchecked(int.MaxValue + y) - (int.MaxValue - x) = y + x;
                // Environment.TickCount rolls over from int.Maxvalue to int.MinValue!
                var deltaMSecs = Environment.TickCount - agentProxy.TimeStamp;
                if (deltaMSecs >= _keepAliveMSecs) {
                    //agentProxy.Post(KeepAliveMessage);
                    // we can use GetState as keep alive message as long as the data size is not too big
                    agentProxy.Post(GetStateMessage);
                }
            }

            // we adjust the checkPeriod so we check multiple times within the keepAlive period;
            // so we can distribute GetStateMessage events more evenly over time, to avoid spikes
            var checkPeriodMSecs = _keepAliveMSecs / (agentCount + 1);
            if (checkPeriodMSecs < 1000)
                checkPeriodMSecs = 1000;
            _keepAliveTimer.Change(checkPeriodMSecs, checkPeriodMSecs);
        }

        public Task<AgentStates> GetAgentStates() {
            var agentStates = new List<AgentState>();
            foreach (var entry in _proxies) {
                if (!entry.Value.IsConnected())
                    continue;
                var agentState = entry.Value.GetState();
                if (agentState != null)
                    agentStates.Add(agentState);
            }
            var result = new AgentStates { Agents = { agentStates } };
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
