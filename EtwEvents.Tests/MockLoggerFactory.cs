using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace EtwEvents.Tests
{
    class MockLoggerFactory: ILoggerFactory
    {
        readonly IExternalScopeProvider _scopeProvider;
        readonly List<MockLogger> _loggers;

        public IExternalScopeProvider ScopeProvider => _scopeProvider;
        public IReadOnlyList<MockLogger> Loggers => _loggers;

        public MockLoggerFactory() {
            _scopeProvider = new LoggerExternalScopeProvider();
            _loggers = new List<MockLogger>();
        }

        public void AddProvider(ILoggerProvider provider) { }
        public ILogger CreateLogger(string categoryName) {
            var result = new MockLogger(categoryName, _scopeProvider);
            _loggers.Add(result);
            return result;
        }

        public void Dispose() {
            _loggers.Clear();
        }
    }
}
