using System;
using Microsoft.Extensions.Logging;

namespace KdSoft.EtwEvents.PushClient
{
    public static class Utils
    {
        public static void LogAllErrors(this ILogger logger, Exception ex, string? message = null) {
            if (logger == null)
                return;
            if (ex is AggregateException aggex) {
                foreach (var iex in aggex.Flatten().InnerExceptions) {
                    logger.LogError(iex, message ?? iex.Message);
                }
            }
            else {
                logger.LogError(ex, message ?? ex.Message);
            }
        }
    }
}
