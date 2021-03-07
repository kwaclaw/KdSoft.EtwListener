using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Elasticsearch.Net;
using Google.Protobuf;
using KdSoft.EtwEvents.Client.Shared;
using KdSoft.EtwLogging;

namespace KdSoft.EtwEvents.EventSinks
{
    public class ElasticSink: IEventSink
    {
        readonly ElasticSinkOptions _sinkInfo;
        readonly string _bulkMeta;
        readonly IConnectionPool _connectionPool;
        readonly TaskCompletionSource<bool> _tcs;
        readonly List<InsertRecord> _evl;
        readonly ElasticLowLevelClient _client;

        int _isDisposed = 0;

        public string Name { get; }

        public Task<bool> RunTask { get; }

        public ElasticSink(string name, ElasticSinkOptions sinkInfo, string dbUser, string dbPwd) {
            this.Name = name;

            _tcs = new TaskCompletionSource<bool>();
            RunTask = _tcs.Task;

            _evl = new List<InsertRecord>();
            _sinkInfo = sinkInfo;
            _bulkMeta = $@"{{ ""index"": {{ ""_index"" : ""{_sinkInfo.Index}"" }} }}";

            try {
                IConnectionPool connectionPool;
                if (sinkInfo.Nodes.Length == 1)
                    connectionPool = new SingleNodeConnectionPool(new Uri(sinkInfo.Nodes[0]));
                else if (sinkInfo.Nodes.Length > 1)
                    connectionPool = new SniffingConnectionPool(sinkInfo.Nodes.Select(node => new Uri(node)));
                else
                    throw new ArgumentException("Must provide at least one ElasticSearch node Uri", nameof(sinkInfo));
                this._connectionPool = connectionPool;

                var config = new ConnectionConfiguration(connectionPool);
                _client = new ElasticLowLevelClient(config);
            }
            catch (Exception ex) {
                var errStr = $@"Error in {nameof(ElasticSink)} initialization encountered:{Environment.NewLine}{ex.Message}";
                //healthReporter.ReportProblem(errStr, EventFlowContextIdentifiers.Configuration);
                throw;
            }
        }

        public bool IsDisposed {
            get {
                Interlocked.MemoryBarrier();
                var isDisposed = this._isDisposed;
                Interlocked.MemoryBarrier();
                return isDisposed > 0;
            }
        }

        public void Dispose() {
            var oldDisposed = Interlocked.CompareExchange(ref _isDisposed, 99, 0);
            if (oldDisposed == 0) {
                _connectionPool.Dispose();
                _tcs.TrySetResult(true);
            }
        }

        // Warning: ValueTasks should not be awaited multiple times
        public ValueTask DisposeAsync() {
            Dispose();
            return ValueTask.CompletedTask;
        }

        public bool Equals([AllowNull] IEventSink other) {
            if (object.ReferenceEquals(this, other))
                return true;
            if (other == null)
                return false;
            return StringComparer.Ordinal.Equals(this.Name, other.Name);
        }

        static IEnumerable<string> EnumerateInsertRecords(List<InsertRecord> irList) {
            for (int indx = 0; indx < irList.Count; indx++) {
                yield return irList[indx].Meta;
                yield return irList[indx].Source;
            }
        }

        async Task<bool> FlushAsyncInternal() {
            var postItems = EnumerateInsertRecords(_evl);
            var bulkResponse = await _client.BulkAsync<StringResponse>(PostData.MultiJson(postItems)).ConfigureAwait(false);

            _evl.Clear();
            if (bulkResponse.Success)
                return true;

            if (bulkResponse.TryGetServerError(out var error)) {
                // log error
            }
            return false;
        }

        public ValueTask<bool> FlushAsync() {
            if (IsDisposed)
                return new ValueTask<bool>(false);
            if (_evl.Count == 0)
                return new ValueTask<bool>(true);
            return new ValueTask<bool>(FlushAsyncInternal());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        InsertRecord FromEvent(EtwEvent evt, long sequenceNo) {
            //TODO should we ignore sequenceNo?
            var bulkSource = JsonFormatter.Default.Format(evt);
            return new InsertRecord(_bulkMeta, bulkSource);
        }

        //TODO maybe use Interlocked and two lists to keep queueing while a bulk write is in process
        public ValueTask<bool> WriteAsync(EtwEvent evt, long sequenceNo) {
            if (IsDisposed)
                return new ValueTask<bool>(false);
            _evl.Add(FromEvent(evt, sequenceNo));
            return new ValueTask<bool>(true);
        }

        public ValueTask<bool> WriteAsync(EtwEventBatch evtBatch, long sequenceNo) {
            if (IsDisposed)
                return new ValueTask<bool>(false);
            _evl.AddRange(evtBatch.Events.Select(evt => FromEvent(evt, sequenceNo++)));
            return new ValueTask<bool>(true);
        }

        struct InsertRecord
        {
            public InsertRecord(string meta, string source) {
                this.Meta = meta;
                this.Source = source;
            }
            public readonly string Meta;
            public readonly string Source;
        }
    }
}
