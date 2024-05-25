using System.Security.Cryptography.X509Certificates;
using Elastic.Transport;
using Elastic.Transport.Products.Elasticsearch;
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
        readonly TaskCompletionSource<bool> _tcs;
        readonly List<string> _evl;
        readonly JsonFormatter _jsonFormatter;

        readonly ITransport _transport;

        int _isDisposed = 0;

        public Task<bool> RunTask { get; }

        public ElasticSink(ElasticSinkOptions options, ElasticSinkCredentials creds, IEventSinkContext context) {
            this._options = options;
            this._logger = context.Logger;
            this._indexFormat = options.IndexFormat.Replace("{site}", context.SiteName).ToLower();

            _tcs = new TaskCompletionSource<bool>();
            RunTask = _tcs.Task;

            _evl = new List<string>();

            try {
                AuthorizationHeader authHeader;
                if (string.IsNullOrEmpty(creds.ApiKey)) {
                    authHeader = new BasicAuthentication(creds.User ?? "anonymous", creds.Password ?? "");
                }
                else if (string.IsNullOrEmpty(creds.ApiKeyId)) {
                    // this must be the already Base64 encoded API key, which does not require an id
                    authHeader = new ApiKey(creds.ApiKey);
                }
                else {  // used for Cloud
                    authHeader = new Base64ApiKey(creds.ApiKeyId, creds.ApiKey);
                }

                X509Certificate2? clientCert = null;
                if (!string.IsNullOrEmpty(creds.SubjectCN)) {
                    clientCert = CertUtils.GetCertificate(StoreName.My, StoreLocation.LocalMachine, "", creds.SubjectCN);
                }

                NodePool? connectionPool = null;
                if (string.IsNullOrWhiteSpace(options.CloudId)) {
                    if (options.Nodes.Length == 1)
                        connectionPool = new SingleNodePool(new Uri(options.Nodes[0]));
                    else if (options.Nodes.Length > 1)
                        connectionPool = new SniffingNodePool(options.Nodes.Select(node => new Uri(node)));
                }
                else if (!string.IsNullOrWhiteSpace(options.CloudId) && authHeader is not null)
                    connectionPool = new CloudNodePool(options.CloudId, authHeader);
                if (connectionPool is null)
                    throw new ArgumentException("Must provide at least one ElasticSearch node Uri, or a cloud id with credentials", nameof(options));

                var settings = new TransportConfiguration(connectionPool).Authentication(authHeader);
                if (clientCert is not null)
                    settings = settings.ClientCertificate(clientCert);
                _transport = new DistributedTransport<TransportConfiguration>(settings);
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
                    // _transport.Dispose();
                }
                catch (Exception ex) {
                    _logger.LogError(ex, "Error closing event sink '{eventSink)}'.", nameof(ElasticSink));
                }
                _tcs.TrySetResult(true);
            }
        }

        // Warning: ValueTasks should not be awaited multiple times
        public ValueTask DisposeAsync() {
            GC.SuppressFinalize(this);
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
            var indexName = string.Format(this._indexFormat, DateTimeOffset.UtcNow);
            var bulkMeta = $@"{{ ""index"": {{ ""_index"" : ""{indexName}"" }} }}";
            var postItems = EnumerateInsertRecords(bulkMeta, _evl);

            var bulkResponse = await _transport.PostAsync<StringResponse>("/_bulk", PostData.MultiJson(postItems)).ConfigureAwait(false);

            _evl.Clear();
            if (bulkResponse.ApiCallDetails.HasSuccessfulStatusCode)
                return true;

            if (bulkResponse.TryGetElasticsearchServerError(out var error) && error.Error != null) {
                throw new ElasticSinkException($"Error sending bulk response in {nameof(ElasticSink)}: {error}.", error);
            }
            else if (bulkResponse.ApiCallDetails.OriginalException is null) {
                throw new ElasticSinkException(bulkResponse.ApiCallDetails.DebugInformation);
            }
            else {
                throw new ElasticSinkException(bulkResponse.ApiCallDetails.DebugInformation, bulkResponse.ApiCallDetails.OriginalException);
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
