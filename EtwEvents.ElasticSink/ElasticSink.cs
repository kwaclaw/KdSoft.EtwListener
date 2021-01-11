using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using KdSoft.EtwEvents.Client.Shared;
using KdSoft.EtwLogging;
using Elasticsearch.Net;
using System.Linq;
using Google.Protobuf;
using System.Runtime.CompilerServices;

namespace KdSoft.EtwEvents.EventSinks
{
    public class ElasticSink: IEventSink
    {
        readonly ElasticSinkOptions _sinkInfo;
        readonly string _bulkMeta;
        readonly ConnectionConfiguration _config;
        readonly TaskCompletionSource<bool> _tcs;
        readonly List<InsertRecord> _evl;
        readonly ElasticLowLevelClient _client;

        CancellationToken _cancelToken;

        public string Name { get; }

        public Task<bool> RunTask { get; }

        public ElasticSink(string name, ElasticSinkOptions sinkInfo, string dbUser, string dbPwd, CancellationToken cancelToken) {
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

                _config = new ConnectionConfiguration(connectionPool);
                _client = new ElasticLowLevelClient(_config);
            }
            catch (Exception ex) {
                var errStr = $@"Error in {nameof(ElasticSink)} initialization encountered:{Environment.NewLine}{ex.Message}";
                //healthReporter.ReportProblem(errStr, EventFlowContextIdentifiers.Configuration);
                throw;
            }
        }

        static IEnumerable<string> EnumerateInsertRecords(List<InsertRecord> irList) {
            for (int indx = 0; indx < irList.Count; indx++) {
                yield return irList[indx].Meta;
                yield return irList[indx].Source;
            }
        }

        async Task<bool> FlushAsyncInternal() {
            if (_cancelToken.IsCancellationRequested) {
                _tcs.TrySetCanceled(_cancelToken);
                return false;
            }

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        InsertRecord FromEvent(EtwEvent evt, long sequenceNo) {
            //TODO should we ignore sequenceNo?
            var bulkSource = JsonFormatter.Default.Format(evt);
            return new InsertRecord(_bulkMeta, bulkSource);
        }

        //TODO maybe use Interlocked and two lists to keep queueing while a bulk write is in process
        public ValueTask<bool> WriteAsync(EtwEvent evt, long sequenceNo) {
            if (_cancelToken.IsCancellationRequested) {
                _tcs.TrySetCanceled(_cancelToken);
                return new ValueTask<bool>(false);
            }
            _evl.Add(FromEvent(evt, sequenceNo));
            return new ValueTask<bool>(true);
        }

        public ValueTask<bool> WriteAsync(EtwEventBatch evtBatch, long sequenceNo) {
            if (_cancelToken.IsCancellationRequested) {
                _tcs.TrySetCanceled(_cancelToken);
                return new ValueTask<bool>(false);
            }
            _evl.AddRange(evtBatch.Events.Select(evt => FromEvent(evt, sequenceNo++)));
            return new ValueTask<bool>(true);
        }

        //TODO catch exceptions here
        public ValueTask<bool> FlushAsync() {
            if (_evl.Count == 0)
                return new ValueTask<bool>(true);
            return new ValueTask<bool>(FlushAsyncInternal());
        }

        // Warning: ValueTasks should not be awaited multiple times
        public ValueTask DisposeAsync() {
            _tcs.TrySetResult(false);
            return default;
        }

        public void Dispose() {
            _tcs.TrySetResult(false);
        }

        public bool Equals([AllowNull] IEventSink other) {
            if (object.ReferenceEquals(this, other))
                return true;
            if (other == null)
                return false;
            return StringComparer.Ordinal.Equals(this.Name, other.Name);
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
