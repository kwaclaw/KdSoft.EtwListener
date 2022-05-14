using System;
using System.Threading;
using System.Threading.Tasks;
using LaunchDarkly.EventSource;
using Microsoft.Extensions.Logging;

namespace KdSoft.EtwEvents.PushAgent
{
    class ControlContext
    {
        readonly ILogger? _logger;

        public ControlContext(EventSource source, ILogger? logger) {
            this.Source = source;
            this._logger = logger;
        }

        public EventSource Source { get; }
        public CancellationTokenRegistration CancelRegistration { get; private set; } = default;
        public Task? SseTask { get; private set; } = default;

        public async Task StopAsync(TaskCompletionSource? tcs = null) {
            // we do not want oldEventSource to be processed through the stoppingToken
            CancelRegistration.Dispose();
            try {
                Source.Close();
                if (SseTask != null) {
                    await SseTask.ConfigureAwait(false);
                }
                tcs?.TrySetResult();
            }
            catch (OperationCanceledException) {
                tcs?.TrySetCanceled();
                _logger?.LogInformation("EventSource was canceled.");

            }
            catch (Exception ex) {
                tcs?.TrySetException(ex);
                _logger?.LogError(ex, "Error in EventSource.");
            }
            finally {
                Source.Dispose();
            }
        }

        public bool Start(CancellationTokenRegistration cancelRegistration) {
            try {
                SseTask = Source.StartAsync();
            }
            catch (Exception ex) {
                _logger?.LogError(ex, "Error starting EventSource.");
                return false;
            }
            this.CancelRegistration = cancelRegistration;
            return true;
        }
    }


}
