using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EtwEvents.Tests
{
    class MockLogger: ILogger
    {
        readonly string _categoryName;
        readonly IExternalScopeProvider? _externalScopeProvider;
        readonly List<object> _entries;
        readonly List<string> _formattedEntries;

        public string Categoryname => _categoryName;
        public List<object> Entries => _entries;
        public List<string> FormattedEntries => _formattedEntries;

        public MockLogger(string categoryName, IExternalScopeProvider? externalScopeProvider) {
            this._categoryName = categoryName;
            this._externalScopeProvider = externalScopeProvider;
            _entries = new List<object>();
            _formattedEntries = new List<string>();
        }

        public IDisposable BeginScope<TState>(TState state) {
            if (_externalScopeProvider != null)
                return _externalScopeProvider.Push(state);
            return MockDisposable.Instance;
        }

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) {
            if (!IsEnabled(logLevel)) {
                return;
            }
            if (formatter == null) {
                throw new ArgumentNullException(nameof(formatter));
            }

            string message = formatter(state, exception);
            if (string.IsNullOrEmpty(message)) {
                return;
            }

            var logEntry = new LogEntry<TState>(logLevel, _categoryName, eventId, state, exception, formatter);
            _entries.Add(logEntry);

            var builder = new StringBuilder($"{_categoryName}[{eventId}]\n");
            void callback(object? scope, StringBuilder sb) {
                sb.Append(" => ");
                if (scope is IEnumerable<KeyValuePair<string, object>> properties) {
                    foreach (KeyValuePair<string, object> pair in properties) {
                        sb.Append(pair.Key).Append(":").Append(pair.Value?.ToString()).Append("|");
                    }
                    sb.Remove(sb.Length - 1, 1);
                    sb.AppendLine();
                }
                else if (scope != null) {
                    sb.AppendLine(scope.ToString());
                }
            }
            _externalScopeProvider?.ForEachScope(callback, builder);

            builder.AppendLine(message);

            if (exception != null) {
                builder.Append(exception).AppendLine();
            }

            _formattedEntries.Add(builder.ToString());
        }
    }

    class MockLogger<T>: MockLogger, ILogger<T>
    {
        public MockLogger(IExternalScopeProvider? externalScopeProvider) : base(typeof(T).Name, externalScopeProvider) {
            //
        }
    }
}
