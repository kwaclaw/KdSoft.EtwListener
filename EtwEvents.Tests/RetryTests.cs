using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using KdSoft.EtwEvents;
using KdSoft.EtwEvents.EventSinks;
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

        #region Backoff Strategy

        void BackoffMaxRetries(TimeSpan startDelay, TimeSpan maxDelay, int maxRetries) {
            var strategy = new BackoffRetryStrategy(startDelay, maxDelay, maxRetries);
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
            var strategy = new BackoffRetryStrategy(startDelay, maxDelay, backoffSpan);
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

        #endregion

        #region Retrier

        class Operation
        {
            readonly int _maxCount;
            int _count;

            public Operation(int maxCount) {
                this._maxCount = maxCount;
            }

            public async ValueTask<bool> Execute() {
                await Task.Delay(100).ConfigureAwait(false);
                var result = ++_count > _maxCount;
                return result;
            }
        }

        [Fact]
        public async Task Retrier() {
            var retryStrategy = new BackoffRetryStrategy(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(800), 20);
            var retrier = new AsyncRetrier<bool>(r => r, retryStrategy);
            var countHolder = new ValueHolder<int>();

            // op.Execute always fails
            var op = new Operation(int.MaxValue);
            var result = await retrier.ExecuteAsync(op.Execute, countHolder).ConfigureAwait(false);
            _output.WriteLine($"Retrier with {countHolder.Value} retries, all failing, duration: {retryStrategy.TotalDelay}");
            Assert.False(result);
            Assert.Equal(countHolder.Value, retryStrategy.TotalRetries);

            // op.Execute succeeds after 10 counts - before retrier gives up
            op = new Operation(10);
            result = await retrier.ExecuteAsync(op.Execute, countHolder).ConfigureAwait(false);
            _output.WriteLine($"Retrier with {countHolder.Value} retries, 10 failing, duration: {retryStrategy.TotalDelay}");
            Assert.True(result);
            Assert.Equal(countHolder.Value, retryStrategy.TotalRetries);

            // op.Execute succeeds after 21 counts - after retrier gives up
            op = new Operation(21);
            result = await retrier.ExecuteAsync(op.Execute, countHolder).ConfigureAwait(false);
            _output.WriteLine($"Retrier with {countHolder.Value} retries, 21 failing, duration: {retryStrategy.TotalDelay}");
            Assert.False(result);
            Assert.Equal(countHolder.Value, retryStrategy.TotalRetries);
        }

        #endregion

        #region RetryProxy

        [Fact]
        public async Task RetryProxyRollingFile() {
            var serializerOptions = new JsonSerializerOptions {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var sinkFactory = new RollingFileSinkFactory();
            var sinkOptions = new RollingFileSinkOptions {
                Directory = @"C:\Temp\TestLogs",
                FileExtension = ".log",
                FileNameFormat = "etw-test-{0:yyyy-MM-dd}",
                FileSizeLimitKB = 64,
                MaxFileCount = 100,
                NewFileOnStartup = true,
                RelaxedJsonEscaping = true
            };
            var sinkCredentials = new RollingFileSinkCredentials();
            var loggerFactory = new MockLoggerFactory();
            var retryStrategy = new BackoffRetryStrategy(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(800), 20);

            var retryProxy = new EventSinkRetryProxy(
                "test-sink-rolling-file",
                JsonSerializer.Serialize(sinkOptions, serializerOptions),
                JsonSerializer.Serialize(sinkCredentials, serializerOptions),
                sinkFactory,
                loggerFactory,
                retryStrategy
            );

            var writeTasks = new List<ValueTask<bool>>();
            for (int i = 0; i < 100; i++) {
                var wt = retryProxy.WriteAsync(new KdSoft.EtwLogging.EtwEvent());
                writeTasks.Add(wt);
            }
            foreach (var wt in writeTasks) {
                if (!wt.IsCompleted) {
                    await wt.ConfigureAwait(false);
                }
            }

            await retryProxy.DisposeAsync().ConfigureAwait(false);
        }

        [Fact]
        public async Task RetryProxyFailingSink() {
            var serializerOptions = new JsonSerializerOptions {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var sinkFactory = new MockSinkFactory();
            var lf1 = (100, TimeSpan.FromMilliseconds(100));
            var lf2 = (200, TimeSpan.FromMilliseconds(90));
            var lf3= (300, TimeSpan.FromMilliseconds(110));
            var lff = (0, TimeSpan.FromMilliseconds(350));
            var sinkOptions = new MockSinkOptions {
                LifeCycles = 
                { lf1, lff, lff, lff, lff, lf2, lf3, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff
                    , lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lf1, lf1, lf1, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff
                    , lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff
                },
            };
            var sinkCredentials = new MockSinkCredentials();
            var loggerFactory = new MockLoggerFactory();
            var retryStrategy = new BackoffRetryStrategy(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(800), 20);

            //TODO how to pass different options each time EventSinkRetryProxy calls CreateEventSink
            //   - make MockSinkOptions cntain an array of options, and let MockSinkFactory pick a different on each Create call
            var retryProxy = new EventSinkRetryProxy(
                "test-sink-failing",
                JsonSerializer.Serialize(sinkOptions, serializerOptions),
                JsonSerializer.Serialize(sinkCredentials, serializerOptions),
                sinkFactory,
                loggerFactory,
                retryStrategy
            );

            // must ne have overlapping writes
            for (int i = 0; i < 100; i++) {
                await retryProxy.WriteAsync(new KdSoft.EtwLogging.EtwEvent()).ConfigureAwait(false);
                if ((i % 10) == 0)
                    await retryProxy.FlushAsync().ConfigureAwait(false);
            }

            await retryProxy.DisposeAsync().ConfigureAwait(false);
        }

        #endregion
    }
}
