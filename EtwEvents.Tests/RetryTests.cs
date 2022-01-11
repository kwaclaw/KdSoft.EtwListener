using System;
using KdSoft.EtwEvents;
using Xunit;
using Xunit.Abstractions;

namespace EtwEvents.Tests
{
    public class RetryTests
    {
        readonly ITestOutputHelper _output;

        public RetryTests(ITestOutputHelper output) {
            this._output = output;
        }

        void BackoffMaxRetries(TimeSpan startDelay, TimeSpan maxDelay, int maxRetries) {
            var strategy = new ExponentialBackoffRetryStrategy(startDelay, maxDelay, maxRetries);
            _output.WriteLine($"Alpha: {strategy.Alpha}");

            var totalDelay = TimeSpan.Zero;
            bool firstIteration = false;
            TimeSpan firstDelay = TimeSpan.Zero;
            TimeSpan lastDelay = TimeSpan.Zero;
            int lastCount = 0;
            while (strategy.NextDelay(out var delay, out var count)) {
                if (!firstIteration) {
                    firstIteration = true;
                    firstDelay = delay;
                }
                lastDelay = delay;
                lastCount = count;
                totalDelay += delay;
                _output.WriteLine($"Retry: {count}: {delay}");
            }
            _output.WriteLine($"Total: {totalDelay}");

            Assert.Equal(maxRetries, lastCount);
            Assert.True((startDelay - firstDelay) < TimeSpan.FromMilliseconds(1));
            Assert.True((maxDelay - lastDelay) < TimeSpan.FromMilliseconds(1));
        }

        [Fact]
        public void BackoffTestMaxRetries() {
            var startDelay = TimeSpan.FromSeconds(1);
            var maxDelay = TimeSpan.FromSeconds(3600);
            int maxRetries = 50;
            BackoffMaxRetries(startDelay, maxDelay, maxRetries);
        }
        [Fact]
        public void BackoffTestMaxRetriesOneSecond() {
            var startDelay = TimeSpan.FromSeconds(1);
            var maxDelay = TimeSpan.FromSeconds(1);
            int maxRetries = 50;
            BackoffMaxRetries(startDelay, maxDelay, maxRetries);
        }

        [Fact]
        public void BackoffTestMaxRetriesOneRetry() {
            var startDelay = TimeSpan.FromSeconds(1);
            var maxDelay = TimeSpan.FromSeconds(3600);
            int maxRetries = 1;
            BackoffMaxRetries(startDelay, maxDelay, maxRetries);
        }

        void BackoffDelaySpan(TimeSpan startDelay, TimeSpan maxDelay, TimeSpan backoffSpan) {
            var strategy = new ExponentialBackoffRetryStrategy(startDelay, maxDelay, backoffSpan);
            _output.WriteLine($"Alpha: {strategy.Alpha}");
            _output.WriteLine($"MaxRetries: {strategy.MaxRetries}");

            var totalDelay = TimeSpan.Zero;
            bool firstIteration = false;
            TimeSpan firstDelay = TimeSpan.Zero;
            TimeSpan lastDelay = TimeSpan.Zero;
            while (strategy.NextDelay(out var delay, out var count)) {
                if (!firstIteration) {
                    firstIteration = true;
                    firstDelay = delay;
                }
                lastDelay = delay;
                totalDelay += delay;
                _output.WriteLine($"Retry: {count}: {delay}");
            }
            _output.WriteLine($"Total: {totalDelay}");

            Assert.True((startDelay - firstDelay) < TimeSpan.FromMilliseconds(1), nameof(firstDelay));
            Assert.True((maxDelay - lastDelay) < TimeSpan.FromMilliseconds(1), nameof(lastDelay));
            Assert.True(totalDelay <= backoffSpan, nameof(totalDelay));
        }

        [Fact]
        public void BackoffTestDelaySpan() {
            var startDelay = TimeSpan.FromSeconds(1);
            var maxDelay = TimeSpan.FromSeconds(3600);
            var backoffSpan = new TimeSpan(0, 6, 37, 04, 031);  // 06:37:04.0308580
            BackoffDelaySpan(startDelay, maxDelay, backoffSpan);
        }

        [Fact]
        public void BackoffTestDelaySpanOneSecondMax() {
            var startDelay = TimeSpan.FromSeconds(1);
            var maxDelay = TimeSpan.FromSeconds(1);
            var backoffSpan = TimeSpan.FromSeconds(1200);
            BackoffDelaySpan(startDelay, maxDelay, backoffSpan);
        }

        [Fact]
        public void BackoffTestDelaySpanMaxTotal() {
            var startDelay = TimeSpan.FromSeconds(1);
            var maxDelay = TimeSpan.FromSeconds(3600);
            var backoffSpan = maxDelay;
            BackoffDelaySpan(startDelay, maxDelay, backoffSpan);
        }

        [Fact]
        public void BackoffTestDelaySpanOnePointFive() {
            var startDelay = TimeSpan.FromSeconds(1);
            var maxDelay = TimeSpan.FromSeconds(3600);
            var backoffSpan = TimeSpan.FromSeconds(5400);
            BackoffDelaySpan(startDelay, maxDelay, backoffSpan);
        }
    }
}
