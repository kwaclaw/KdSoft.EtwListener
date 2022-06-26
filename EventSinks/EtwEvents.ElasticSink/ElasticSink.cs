using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Elasticsearch.Net;
using Google.Protobuf;
using KdSoft.EtwLogging;
using Microsoft.Extensions.Logging;

namespace KdSoft.EtwEvents.EventSinks
{
    public class ElasticSink: IEventSink
    {
        readonly ElasticSinkOptions _options;
        readonly ILogger _logger;
        readonly string _indexFormat;
        readonly IConnectionPool _connectionPool;
        readonly TaskCompletionSource<bool> _tcs;
        readonly List<string> _evl;
        readonly ElasticLowLevelClient _client;
        readonly JsonFormatter _jsonFormatter;

        int _isDisposed = 0;

        public Task<bool> RunTask { get; }

        public ElasticSink(ElasticSinkOptions options, ElasticSinkCredentials creds, IEventSinkContext context) {
            this._options = options;
            this._logger = context.Logger;
            this._indexFormat = options.IndexFormat.Replace("{site}", context.SiteName);

            _tcs = new TaskCompletionSource<bool>();
            RunTask = _tcs.Task;

            _evl = new List<string>();

            try {
                IConnectionPool connectionPool;
                if (options.Nodes.Length == 1)
                    connectionPool = new SingleNodeConnectionPool(new Uri(options.Nodes[0]));
                else if (options.Nodes.Length > 1)
                    connectionPool = new SniffingConnectionPool(options.Nodes.Select(node => new Uri(node)));
                else
                    throw new ArgumentException("Must provide at least one ElasticSearch node Uri", nameof(options));
                this._connectionPool = connectionPool;

                var config = new ConnectionConfiguration(connectionPool);

                if (!string.IsNullOrEmpty(creds.SubjectCN)) {
                    var clientCert = CertUtils.GetCertificate(StoreLocation.LocalMachine, "", creds.SubjectCN);
                    if (clientCert != null)
                        config.ClientCertificate(clientCert);
                }
                if (!string.IsNullOrEmpty(creds.ApiKey)) {
                    ApiKeyAuthenticationCredentials apiCreds;
                    if (string.IsNullOrEmpty(creds.ApiKeyId))
                        // this must be the base64 encoded API key, which does not require an id
                        apiCreds = new ApiKeyAuthenticationCredentials(creds.ApiKey);
                    else
                        apiCreds = new ApiKeyAuthenticationCredentials(creds.ApiKeyId, creds.ApiKey);
                    config.ApiKeyAuthentication(apiCreds);
                }
                if (!string.IsNullOrEmpty(creds.User) && creds.Password != null) {
                    config.BasicAuthentication(creds.User, creds.Password);
                }
                _client = new ElasticLowLevelClient(config);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error in {eventSink} initialization.", nameof(ElasticSink));
                throw;
            }

            var jsonSettings = JsonFormatter.Settings.Default.WithFormatDefaultValues(true).WithFormatEnumsAsIntegers(true);
            _jsonFormatter = new JsonFormatter(jsonSettings);
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
                try {
                    _connectionPool.Dispose();
                }
                catch (Exception ex) {
                    _logger.LogError(ex, "Error closing event sink '{eventSink)}'.", nameof(ElasticSink));
                }
                _tcs.TrySetResult(true);
            }
        }

        // Warning: ValueTasks should not be awaited multiple times
        public ValueTask DisposeAsync() {
            Dispose();
            return default;
        }

        static IEnumerable<string> EnumerateInsertRecords(string bulkMeta, List<string> irList) {
            for (int indx = 0; indx < irList.Count; indx++) {
                yield return bulkMeta;
                yield return irList[indx];
            }
        }

        async Task<bool> FlushAsyncInternal() {
            var bulkMeta = $@"{{ ""index"": {{ ""_index"" : ""{string.Format(this._indexFormat, DateTimeOffset.UtcNow)}"" }} }}";
            var postItems = EnumerateInsertRecords(bulkMeta, _evl);
            var bulkResponse = await _client.BulkAsync<StringResponse>(PostData.MultiJson(postItems)).ConfigureAwait(false);

            _evl.Clear();
            if (bulkResponse.Success)
                return true;

            if (bulkResponse.TryGetServerError(out var error) && error.Error != null) {
                throw new ElasticSinkException($"Error sending bulk response in {nameof(ElasticSink)}.", error);
            }
            else {
                throw new ElasticSinkException(bulkResponse.DebugInformation, bulkResponse.OriginalException);
            }
        }

        public async ValueTask<bool> WriteAsync(EtwEventBatch evtBatch) {
            if (IsDisposed || RunTask.IsCompleted)
                return false;
            try {
                _evl.AddRange(evtBatch.Events.Select(evt => _jsonFormatter.Format(evt)));
                // flush
                return await FlushAsyncInternal().ConfigureAwait(false);
            }
            catch (Exception ex) {
                _tcs.TrySetException(ex);
                return false;
            }
        }
    }
}
