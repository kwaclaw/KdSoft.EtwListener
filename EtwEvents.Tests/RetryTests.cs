using System.Diagnostics;
using System.Text.Json;
using Google.Protobuf.WellKnownTypes;
using KdSoft.EtwEvents.EventSinks;
using KdSoft.EtwLogging;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace KdSoft.EtwEvents.Tests
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
            var retryHolder = new ValueHolder<RetryStatus>();

            // op.Execute always fails
            var op = new Operation(int.MaxValue);
            var result = await retrier.ExecuteAsync(op.Execute, retryHolder).ConfigureAwait(false);
            _output.WriteLine($"Retrier with {retryHolder.Value.NumRetries} retries, all failing, duration: {retryStrategy.TotalDelay}");
            Assert.False(result);
            Assert.Equal(retryHolder.Value.NumRetries, retryStrategy.TotalRetries);

            // op.Execute succeeds after 10 counts - before retrier gives up
            op = new Operation(10);
            result = await retrier.ExecuteAsync(op.Execute, retryHolder).ConfigureAwait(false);
            _output.WriteLine($"Retrier with {retryHolder.Value.NumRetries} retries, 10 failing, duration: {retryStrategy.TotalDelay}");
            Assert.True(result);
            Assert.Equal(retryHolder.Value.NumRetries, retryStrategy.TotalRetries);

            // op.Execute succeeds after 21 counts - after retrier gives up
            op = new Operation(21);
            result = await retrier.ExecuteAsync(op.Execute, retryHolder).ConfigureAwait(false);
            _output.WriteLine($"Retrier with {retryHolder.Value.NumRetries} retries, 21 failing, duration: {retryStrategy.TotalDelay}");
            Assert.False(result);
            Assert.Equal(retryHolder.Value.NumRetries, retryStrategy.TotalRetries);
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
                MaxFileCount = 10,
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
                null,
                retryStrategy,
                "default",
                loggerFactory
            );

            // must not have overlapping writes
            var batch = new EtwEventBatch();
            for (int i = 1; i <= 8000; i++) {
                var evt = new KdSoft.EtwLogging.EtwEvent() { Id = (uint)i, TimeStamp = DateTimeOffset.UtcNow.ToTimestamp() };
                batch.Events.Clear();
                batch.Events.Add(evt);
                await retryProxy.WriteAsync(batch).ConfigureAwait(false);
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
            var lf3 = Tuple.Create(17, TimeSpan.FromMilliseconds(110));
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

            var sinkCredentials = new MockSinkCredentials();
            var loggerFactory = new MockLoggerFactory();
            var retryStrategy = new BackoffRetryStrategy(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(600), 15);

            //TODO figure out what to measure and assert - e.g. 
            var retryProxy = new EventSinkRetryProxy(
                "test-sink-failing",
                JsonSerializer.Serialize(sinkOptions, serializerOptions),
                JsonSerializer.Serialize(sinkCredentials, serializerOptions),
                sinkFactory,
                null,
                retryStrategy,
                "default",
                loggerFactory
            );

            var testLogger = loggerFactory.CreateLogger("retry-test");
            var writeLoops = new List<(EtwEvent evt, bool success, TimeSpan elapsed, int retries, TimeSpan delay)>();
            var batch = new EtwEventBatch();
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            // must not have overlapping writes
            for (int i = 0; i < 240; i++) {
                // if Id = 0 it won't be stored (as it is a default value in protobuf)
                var evt = new KdSoft.EtwLogging.EtwEvent() { Id = (uint)(i + 1), TimeStamp = DateTimeOffset.UtcNow.ToTimestamp() };
                batch.Events.Clear();
                batch.Events.Add(evt);
                using (var scope = testLogger.BeginScope("WriteAsync: {scopeType}-{scopeId}", "w", i)) {
                    retryStrategy.Reset();
                    stopWatch.Restart();
                    // when a sinks WriteAsync() succeeds then the same sink's remaining writes will be used for the next events! 
                    var success = await retryProxy.WriteAsync(batch).ConfigureAwait(false);
                    var elapsed = stopWatch.Elapsed;
                    if (!success) {
                        _output.WriteLine($"Failed write: {i}, retries: {retryStrategy.TotalRetries}, delay: {retryStrategy.TotalDelay.TotalMilliseconds}");
                    }
                    writeLoops.Add((evt, success, elapsed, retryStrategy.TotalRetries, retryStrategy.TotalDelay));
                }
            }

            await retryProxy.DisposeAsync().ConfigureAwait(false);
            await retryProxy.RunTask.ConfigureAwait(false);

            _output.WriteLine("");
            _output.WriteLine($"Total life cycles: {sinkFactory.SinkLifeCycles.Count}");

            for (int indx = 0; indx < sinkFactory.SinkLifeCycles.Count; indx++) {
                var lc = sinkFactory.SinkLifeCycles[indx];
                var ocIndx = indx % sinkOptions.LifeCycles.Count;
                var oc = sinkOptions.LifeCycles[ocIndx];

                Assert.Equal(oc.Item1, lc.MaxWrites);
                Assert.Equal(oc.Item2, lc.CallDuration);

                Assert.True(lc.Disposed);
                // On the last WriteAsync() we may not use up all allowed writes, so lc.MaxWrites could be < lc.WriteCount;
                // Note: even a failed write increments the write count, so lc.WriteCount will be > 0
                if (lc.WriteCount <= lc.MaxWrites)
                    _output.WriteLine($"Not all writes were used for life cycle {indx} (max|actual): {lc.MaxWrites} | {lc.WriteCount}");
                else if (lc.WriteCount > (lc.MaxWrites + 1))
                    _output.WriteLine($"Extra writes were used for life cycle {indx} (max|actual): {lc.MaxWrites} | {lc.WriteCount}");
            }
            _output.WriteLine("");

            var lifeCycleGroups = sinkFactory.SinkLifeCycles.Where(lc => lc.Event != null).GroupBy(lc => lc.Event!.Id);
            var lifeCycleMap = lifeCycleGroups.ToDictionary(grp => grp.Key);

            for (int indx = 0; indx < 240; indx++) {
                bool hasGroup = lifeCycleMap.TryGetValue((uint)(indx + 1), out var lcGroup);
                var loopEntry = writeLoops[indx];

                int lifeCycleCount = 0;
                if (hasGroup) {
                    lifeCycleCount = lcGroup!.Count();
                }

                // if life-cycle count > retry count then the WriteAsync failed
                _output.WriteLine($"[{indx}]-{loopEntry.success} Lifecycles: {lifeCycleCount} | {loopEntry.elapsed.TotalMilliseconds},\tRetries: {loopEntry.retries} | {loopEntry.delay.TotalMilliseconds}");

                // tolerate difference between elasped time and total calculated retry delay of up to 330ms
                var diffTime = loopEntry.elapsed - loopEntry.delay;
                Assert.True(diffTime.TotalMilliseconds < 330,
                    $"At loop index {indx} (elapsed | delay): {loopEntry.elapsed.TotalMilliseconds} | {loopEntry.delay.TotalMilliseconds}");
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
