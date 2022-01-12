namespace KdSoft.EtwEvents
{
    /// <summary>
    /// Implements exponential backoff strategy, based on a starting delay and maximum delay.
    /// Can also be configured to have no backoff, and to run forever.
    /// </summary>
    /// <remarks>
    /// We follow this equation to calculate the delay, when only maxRetries is given:
    ///   nextDelay = startDelay * e^(retryIndex*alpha), where alpha is an internally
    ///   calculated factor based on maximum delay and maximum retries.
    /// When only delaySpan is given, we follow the two equations below to calculate alpha:
    ///   alpha = log(maxDelay / startDelay) / maxRetries
    ///   Sum[k= 1 to max_retries] {startDelay * e^(k*alpha)} = delaySpan
    /// </remarks>
    public class BackoffRetryStrategy: IRetryStrategy
    {
        readonly TimeSpan _startDelay;
        readonly TimeSpan _maxDelay;
        readonly bool _forever;
        readonly int _maxRetries;
        readonly double _alpha;

        public TimeSpan StartDelay => _startDelay;
        public TimeSpan MaxDelay => _maxDelay;
        public bool Forever => _forever;
        public int MaxRetries => _maxRetries;
        public double Alpha => _alpha;

        int _retries = 1;
        public int TotalRetries => _retries - 1;

        TimeSpan _totalDelay = TimeSpan.Zero;
        public TimeSpan TotalDelay => _totalDelay;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="startDelay">Starting delay for exponential backoff.</param>
        /// <param name="maxDelay">Maximum delay for exponential backoff.</param>
        /// <param name="maxRetries">Number of retries after which the maximum delay is achieved.</param>
        /// <param name="forever">
        ///     Indicates if NextDelay() should continue calculating new delays
        ///     forever even if the maximum delay was already arrived at.
        /// </param>
        public BackoffRetryStrategy(TimeSpan startDelay, TimeSpan maxDelay, int maxRetries, bool forever = false) {
            if (maxDelay < startDelay)
                throw new ArgumentException("Max delay must not be less than startDelay", nameof(maxDelay));
            if (maxRetries < 1)
                throw new ArgumentException("Max retries must not be less than 1", nameof(maxRetries));

            this._startDelay = startDelay;
            this._maxDelay = maxDelay;
            this._maxRetries = maxRetries;
            this._forever = forever;

            var maxNormalized = (double)maxDelay.Ticks / (double)startDelay.Ticks;
            _alpha = Math.Log(maxNormalized) / maxRetries;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="startDelay">Starting delay for exponential backoff.</param>
        /// <param name="maxDelay">Maximum delay for exponential backoff.</param>
        /// <param name="backoffSpan">
        ///     Timespan after which <paramref name="maxDelay"/> is achieved.
        ///     After this, the behavior of NextDelay() depends on <paramref name="forever"/>.
        /// </param>
        /// <param name="forever">
        ///     Indicates if NextDelay() should continue calculating new delays
        ///     forever even if the maximum delay was already achieved.
        /// </param>
        public BackoffRetryStrategy(TimeSpan startDelay, TimeSpan maxDelay, TimeSpan backoffSpan, bool forever = false) {
            if (maxDelay < startDelay)
                throw new ArgumentException("Max delay must not be less than startDelay", nameof(maxDelay));
            if (backoffSpan < maxDelay)
                throw new ArgumentException("Backoff span must not be less than maxDelay", nameof(backoffSpan));

            this._startDelay = startDelay;
            this._maxDelay = maxDelay;
            this._forever = forever;


            var maxDelayNormalized = (double)maxDelay.Ticks / (double)startDelay.Ticks;
            var delaySpanNormalized = (double)backoffSpan.Ticks / (double)startDelay.Ticks;
            for (var maxRetriesTemp = 1; maxRetriesTemp < 1000000; maxRetriesTemp++) {
                double delaySpanTemp = 0;
                var alphaTemp = Math.Log(maxDelayNormalized) / maxRetriesTemp;
                for (var k = 1; k <= maxRetriesTemp; k++) {
                    delaySpanTemp += Math.Exp(alphaTemp * k);
                }
                if (delaySpanTemp > delaySpanNormalized) {
                    break;
                }
                _alpha = alphaTemp;
                _maxRetries = maxRetriesTemp;
            }
        }

        public bool NextDelay(out TimeSpan delay, out int count) {
            bool doRetry = true;
            int retryIndex = _maxRetries;
            if (_retries <= _maxRetries) {
                retryIndex = _retries;
            }
            else {
                doRetry = _forever;
            }

            if (doRetry) {
                count = _retries++;
                var delayTicks = Math.Exp(_alpha * retryIndex) * _startDelay.Ticks;
                delay = new TimeSpan((long)delayTicks);
                _totalDelay += delay;
            }
            else {
                count = 0;
                delay = TimeSpan.Zero;
            }

            return doRetry;
        }

        public void Reset() {
            _retries = 1;
            _totalDelay = TimeSpan.Zero;
        }
    }
}
