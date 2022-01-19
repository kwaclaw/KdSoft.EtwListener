using Microsoft.Extensions.Logging;

namespace EtwEvents.Tests
{
    class MockLoggerFactory: ILoggerFactory {
        public void AddProvider(ILoggerProvider provider) { }
        public ILogger CreateLogger(string categoryName) => new MockLogger();
        public void Dispose() { }
    }
}
