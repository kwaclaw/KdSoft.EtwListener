using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grpc.Core;
using KdSoft.EtwEvents.Server;
using KdSoft.EtwLogging;
using Microsoft.Extensions.Logging;

namespace KdSoft.EtwEvents.Server
{
    class ChannelEventStreamProcessor: ChannelEventProcessor
    {
        readonly IServerStreamWriter<EtwEventBatch> _responseStream;
        readonly WriteOptions _flushWriteOptions;


        public ChannelEventStreamProcessor(
            IServerStreamWriter<EtwEventBatch> responseStream,
            ILogger logger,
            int batchSize = 100,
            int maxWriteDelayMSecs = 400
        ): base(logger, batchSize, maxWriteDelayMSecs) {
            this._responseStream = responseStream;
            _flushWriteOptions = new WriteOptions(WriteFlags.NoCompress);
        }

        protected override async ValueTask<bool> WriteBatchAsync(EtwEventBatch batch) {
            _responseStream.WriteOptions = _flushWriteOptions;
            await _responseStream.WriteAsync(batch).ConfigureAwait(false);
            return true;
        }
    }
}
