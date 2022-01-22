using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using KdSoft.EtwEvents;
using KdSoft.EtwEvents.EventSinks;
using KdSoft.EtwLogging;
using Microsoft.Extensions.Logging;
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

            public async ValueTask<bool> Execute(int retryNum, TimeSpan delay) {
                await Task.Delay(100).ConfigureAwait(false);
                var result = ++_count > _maxCount;
                return result;
            }
        }

        [Fact]
        public async Task Retrier() {
            var retryStrategy = new BackoffRetryStrategy(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(800), 20);
            var retrier = new AsyncRetrier<bool>(r => r, retryStrategy);
            var retryHolder = new ValueHolder<int, TimeSpan>();

            // op.Execute always fails
            var op = new Operation(int.MaxValue);
            var result = await retrier.ExecuteAsync(op.Execute, retryHolder).ConfigureAwait(false);
            _output.WriteLine($"Retrier with {retryHolder.Value1} retries, all failing, duration: {retryStrategy.TotalDelay}");
            Assert.False(result);
            Assert.Equal(retryHolder.Value1, retryStrategy.TotalRetries);

            // op.Execute succeeds after 10 counts - before retrier gives up
            op = new Operation(10);
            result = await retrier.ExecuteAsync(op.Execute, retryHolder).ConfigureAwait(false);
            _output.WriteLine($"Retrier with {retryHolder.Value1} retries, 10 failing, duration: {retryStrategy.TotalDelay}");
            Assert.True(result);
            Assert.Equal(retryHolder.Value1, retryStrategy.TotalRetries);

            // op.Execute succeeds after 21 counts - after retrier gives up
            op = new Operation(21);
            result = await retrier.ExecuteAsync(op.Execute, retryHolder).ConfigureAwait(false);
            _output.WriteLine($"Retrier with {retryHolder.Value1} retries, 21 failing, duration: {retryStrategy.TotalDelay}");
            Assert.False(result);
            Assert.Equal(retryHolder.Value1, retryStrategy.TotalRetries);
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

            // must not have overlapping writes
            for (int i = 1; i <= 100; i++) {
                var evt = new KdSoft.EtwLogging.EtwEvent() { Id = (uint)i, TimeStamp = DateTimeOffset.UtcNow.ToTimestamp() };
                await retryProxy.WriteAsync(evt).ConfigureAwait(false);
                if ((i % 10) == 0) {
                    await retryProxy.FlushAsync().ConfigureAwait(false);
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

            // using Tuples instead of ValueTuples, as the latter won't be serialized into JSON
            var lf1 = Tuple.Create(6, TimeSpan.FromMilliseconds(100));
            var lf2 = Tuple.Create(11, TimeSpan.FromMilliseconds(90));
            var lf3= Tuple.Create(17, TimeSpan.FromMilliseconds(110));
            var lff = Tuple.Create(0, TimeSpan.FromMilliseconds(350));
            var sinkOptions = new MockSinkOptions {
                LifeCycles = { 
                    lf1, lff, lff, lff, lff, lf2, lf3, lff, lff, lff, lff, lff, lff, lff, lff, lff,
                    lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff,
                    lff, lff, lff, lf1, lf1, lf1, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff,
                    lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff,
                    lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lff, lf2, lff,
                },
            };
            var totalMaxWrites = sinkOptions.LifeCycles.Select(lc => lc.Item1).Aggregate<int>((x,y) => x + y);

            var sinkCredentials = new MockSinkCredentials();
            var loggerFactory = new MockLoggerFactory();
            var retryStrategy = new BackoffRetryStrategy(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(600), 15);

            //TODO figure out what to measure and assert - e.g. 
            var retryProxy = new EventSinkRetryProxy(
                "test-sink-failing",
                JsonSerializer.Serialize(sinkOptions, serializerOptions),
                JsonSerializer.Serialize(sinkCredentials, serializerOptions),
                sinkFactory,
                loggerFactory,
                retryStrategy
            );

            var testLogger = loggerFactory.CreateLogger("retry-test");
            var writeLoops = new List<(EtwEvent evt, int retries,TimeSpan delay)>();
            // must not have overlapping writes
            for (int i = 0; i < 240; i++) {
                // if Id = 0 it won't be stored (as it is a default value in protobuf)
                var evt = new KdSoft.EtwLogging.EtwEvent() { Id = (uint)(i+1), TimeStamp = DateTimeOffset.UtcNow.ToTimestamp() };
                using (var scope = testLogger.BeginScope("WriteAsync {evt}: {scopeType}-{scopeId}", evt, "w", i)) {
                    retryStrategy.Reset();
                    await retryProxy.WriteAsync(evt).ConfigureAwait(false);
                    writeLoops.Add((evt, retryStrategy.TotalRetries, retryStrategy.TotalDelay));
                }
                if ((i % 10) == 0) {
                    using (var scope = testLogger.BeginScope("FlushAsync {Id}|{timestamp}: {scopeType}-{scopeId}", i+1, DateTimeOffset.UtcNow, "f", i)) {
                        retryStrategy.Reset();
                        await retryProxy.FlushAsync().ConfigureAwait(false);
                        var loopEntry = writeLoops[i];
                        loopEntry.retries += retryStrategy.TotalRetries;
                        loopEntry.delay += retryStrategy.TotalDelay;
                        writeLoops[i] = loopEntry;
                    }
                }
            }

            await retryProxy.DisposeAsync().ConfigureAwait(false);

            _output.WriteLine($"Total life cycles: {sinkFactory.SinkLifeCycles.Count}, total max retries: {totalMaxWrites}");

            for (int indx = 0; indx < sinkFactory.SinkLifeCycles.Count; indx++) {
                var lc = sinkFactory.SinkLifeCycles[indx];
                var ocIndx = indx % sinkOptions.LifeCycles.Count;
                var oc = sinkOptions.LifeCycles[ocIndx];

                Assert.Equal(oc.Item1, lc.MaxWrites);
                Assert.Equal(oc.Item2, lc.CallDuration);

                Assert.True(lc.Disposed);
                Assert.Equal(lc.MaxWrites, lc.WriteCount);
            }

            var lifeCycleGroups = sinkFactory.SinkLifeCycles.Where(lc => lc.Event != null).GroupBy(lc => lc.Event!.Id);
            var lifeCycleMap = lifeCycleGroups.ToDictionary(grp => grp.Key);
            //Assert.Equal(240, lifeCycleGroups.Count);

            for (int indx = 0; indx < 240; indx++) {
                bool hasGroup = lifeCycleMap.TryGetValue((uint)(indx + 1), out var lcGroup);
                var loopEntry = writeLoops[indx];

                int lifeCycleCount = 0;
                TimeSpan totalLifeSpan = TimeSpan.Zero;
                if (hasGroup) {
                    lifeCycleCount = lcGroup!.Count();
                    totalLifeSpan = lcGroup!.Aggregate(TimeSpan.Zero, (t, y) => t + y.LifeSpan);
                }

                _output.WriteLine($"[{indx}] Lifecycles: {lifeCycleCount} | {totalLifeSpan.TotalMilliseconds},\tRetries: {loopEntry.retries} | {loopEntry.delay.TotalMilliseconds}");
            }

            _output.WriteLine("");

            foreach (var logger in loggerFactory.Loggers) {
                foreach (var formatted in logger.FormattedEntries) {
                    _output.WriteLine(formatted);
                }
            }

            loggerFactory.Dispose();
        }

        #endregion
    }
}
