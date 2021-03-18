using System;
using System.Text;
using Microsoft.Extensions.Logging;

namespace KdSoft.Logging
{
    public static class LogStringBuilderExtension
    {
        static readonly string _messagePadding = new string(' ', MessagePaddingWidth);
        static readonly string _newLineWithMessagePadding = Environment.NewLine + _messagePadding;

        static int MessagePaddingWidth => 6;

        public static string GetLogLevelString(LogLevel logLevel) {
            switch (logLevel) {
                case LogLevel.Trace:
                    return "trce";
                case LogLevel.Debug:
                    return "dbug";
                case LogLevel.Information:
                    return "info";
                case LogLevel.Warning:
                    return "warn";
                case LogLevel.Error:
                    return "fail";
                case LogLevel.Critical:
                    return "crit";
                default:
                    throw new ArgumentOutOfRangeException(nameof(logLevel));
            }
        }

        public static void AppendLogScope(this StringBuilder sb, object scope) {
            sb.Append("=> ").Append(scope);
        }

        public static void AppendLogScopeInfo(this StringBuilder sb, IExternalScopeProvider scopeProvider) {
            var initialLength = sb.Length;

            scopeProvider.ForEachScope((scope, state) => {
                (StringBuilder builder, int length) = state;

                var first = length == builder.Length;
                if (!first)
                    builder.Append(' ');

                AppendLogScope(builder, scope);
            }, (sb, initialLength));

            if (sb.Length > initialLength) {
                sb.Insert(initialLength, _messagePadding);
                sb.AppendLine();
            }
        }

        public static void AppendMessage(this StringBuilder sb, string message) {
            sb.Append(_messagePadding);

            var length = sb.Length;
            sb.AppendLine(message);
            sb.Replace(Environment.NewLine, _newLineWithMessagePadding, length, message.Length);
        }

        public static void BuildLogMessage(
            this StringBuilder sb,
            string categoryName,
            LogLevel logLevel,
            EventId eventId,
            string message,
            Exception exception,
            IExternalScopeProvider? scopeProvider,
            DateTimeOffset timestamp
        ) {
            sb.Append(GetLogLevelString(logLevel));

            var suffix = timestamp.Offset == TimeSpan.Zero ? "Z: " : ": ";
            sb.Append(" @ ").Append(timestamp.DateTime.ToString("s")).Append(suffix);

            sb.Append(categoryName).Append('[').Append(eventId).AppendLine("]");

            if (scopeProvider != null)
                AppendLogScopeInfo(sb, scopeProvider);

            if (!string.IsNullOrEmpty(message))
                AppendMessage(sb, message);

            if (exception != null)
                sb.AppendLine(exception.ToString());
        }
    }
}
