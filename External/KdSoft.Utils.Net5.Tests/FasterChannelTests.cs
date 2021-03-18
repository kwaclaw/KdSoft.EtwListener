using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KdSoft.Utils;
using Xunit;

namespace EtwEvents.Tests
{
    public class FasterChannelTests
    {
        static readonly Memory<byte> _batchSentinel = new byte[4];
        static readonly Memory<byte> _stopSentinel = new byte[7];

        async Task<List<List<Guid>>> ReadChannelAsync(FasterChannel channel, CancellationToken stoppingToken = default) {
            bool isCompleted;
            var result = new List<List<Guid>>();

            using (var reader = channel.GetNewReader()) {
                do {
                    isCompleted = true;
                    var batch = new List<Guid>();

                    await foreach (var (owner, length) in reader.ReadAllAsync(stoppingToken).ConfigureAwait(false)) {
                        using (owner) {
                            // this record indicates we should write the batch even if incomplete
                            if (length == _batchSentinel.Length) {
                                if (batch.Count == 0)
                                    continue;
                                isCompleted = false;
                                break;
                            }
                            // this record indicates we are at the end of writing
                            else if (length == _stopSentinel.Length) {
                                break;
                            }

                            var item = new Guid(owner.Memory.Span.Slice(0, length));
                            batch.Add(item);
                        }
                    }
                    if (batch.Count > 0)
                        result.Add(batch);

                    // simulate that we are doing something with the record
                    await Task.Delay(100).ConfigureAwait(false);

                    // remove record from log, we are done with it
                    reader.Truncate();
                    await channel.CommitAsync().ConfigureAwait(false);
                } while (!isCompleted);
            }

            return result;
        }

        List<List<Guid>> ReadChannelSync(FasterChannel channel, CancellationToken stoppingToken = default) {
            bool isCompleted;
            var result = new List<List<Guid>>();

            using (var reader = channel.GetNewReader()) {
                do {
                    isCompleted = true;
                    var batch = new List<Guid>();

                    while (reader.TryRead(out var owner, out var length)) {
                        using (owner) {
                            // this record indicates we should write the batch even if incomplete
                            if (length == _batchSentinel.Length) {
                                if (batch.Count == 0)
                                    continue;
                                isCompleted = false;
                                break;
                            }
                            // this record indicates we are at the end of writing
                            else if (length == _stopSentinel.Length) {
                                break;
                            }

                            var item = new Guid(owner.Memory.Span.Slice(0, length));
                            batch.Add(item);
                        }
                    }
                    if (batch.Count > 0)
                        result.Add(batch);

                    // simulate that we are doing something with the record
                    Thread.Sleep(100);

                    // remove record from log, we are done with it
                    reader.Truncate();
                    channel.Commit(true);
                } while (!isCompleted);
            }

            return result;
        }

        [Fact]
        public async void BasicWriteAndReadLoop() {
            var inputData = new List<Guid>();
            for (int i = 0; i <= 10000; i++) {
                inputData.Add(Guid.NewGuid());
            }

            List<List<Guid>> outputData;
            int batchCount = 0;

            using (var channel = new FasterChannel(@"C:\Temp\FasterLog\hhlog.log")) {
                // catch up to end of channel
                var recoveredData = ReadChannelSync(channel);

                // must be started on a new Thread
                var readerTask = Task.Run(() => ReadChannelAsync(channel));

                int counter = 0;
                bool written;
                foreach (var item in inputData) {
                    //var bytes = BitConverter.GetBytes(item);
                    var bytes = item.ToByteArray();
                    written = channel.TryWrite(bytes);
                    Assert.True(written);

                    counter++;
                    if (counter >= 100) {
                        counter = 0;

                        written = channel.TryWrite(_batchSentinel);
                        Assert.True(written);
                        channel.Commit(true);

                        await Task.Delay(100).ConfigureAwait(false);

                        batchCount++;
                    }
                }
                if (counter > 0) {
                    written = channel.TryWrite(_batchSentinel);
                    Assert.True(written);
                    channel.Commit(true);
                    batchCount++;
                }

                written = channel.TryWrite(_stopSentinel);
                Assert.True(written);
                channel.Commit(true);

                outputData = await readerTask.ConfigureAwait(false);
            }

            Assert.True(batchCount == outputData.Count);
            Assert.Equal(inputData, outputData.SelectMany<List<Guid>, Guid>(od => od));
        }
    }
}
