using System;

namespace KdSoft.EtwEvents.PushAgent
{
    public class ControlOptions
    {
        public Uri Uri { get; set; } = new Uri("http://example.com");
        /// <summary>
        /// Sets the initial amount of time to wait before attempting to reconnect to the EventSource API.
        /// If the connection fails more than once, the retry delay will increase from this value using
        /// a backoff algorithm. If not specified, defaults to 1 second.
        /// </summary>
        public TimeSpan? InitialRetryDelay { get; set; }
        /// <summary>
        /// <c>EventSource</c> uses an exponential backoff algorithm (with random jitter) so that
        /// the delay between reconnections starts at <see cref="InitialRetryDelay(TimeSpan)"/> but
        /// increases with each subsequent attempt. <c>MaxRetryDelay</c> sets a limit on how long
        /// the delay can be. If not specified, defaults to 30 seconds.
        /// </summary>
        public TimeSpan? MaxRetryDelay { get; set; }
        /// <summary>
        /// If a connection fails before the threshold has elapsed, the delay before reconnecting will be greater
        /// than the last delay; if it fails after the threshold, the delay will start over at the initial minimum
        /// value. This prevents long delays from occurring on connections that are only rarely restarted.
        /// If not specified, defaults to 1 minute.
        /// </summary>
        public TimeSpan? BackoffResetThreshold { get; set; }

        public ClientCertOptions ClientCertificate { get; set; } = new ClientCertOptions();
    }
}
