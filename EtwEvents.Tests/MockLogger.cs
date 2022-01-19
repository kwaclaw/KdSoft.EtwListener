using System;
using Microsoft.Extensions.Logging;

namespace EtwEvents.Tests
{
    class MockLogger: ILogger
    {
        public IDisposable BeginScope<TState>(TState state) => new MockDisposable();
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) {
            //
        }
    }
}
