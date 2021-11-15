using System;
using Microsoft.Extensions.Logging;

namespace KdSoft.EtwEvents.PushAgent
{
    public static class Utils
    {
        /// <summary>
        /// Logs exception drilling down into inner exceptions and base exceptions.
        /// </summary>
        /// <param name="logger">Logger instance.</param>
        /// <param name="ex">Exception to log.</param>
        /// <param name="message">Error message.</param>
        public static void LogAllErrors(this ILogger logger, Exception ex, string? message = null) {
            if (logger == null)
                return;
            if (ex is AggregateException aggex) {
                foreach (var iex in aggex.Flatten().InnerExceptions) {
                    logger.LogError(iex.GetBaseException(), message ?? iex.Message);
                }
            }
            else {
                logger.LogError(ex.GetBaseException(), message ?? ex.Message);
            }
        }
    }
}
