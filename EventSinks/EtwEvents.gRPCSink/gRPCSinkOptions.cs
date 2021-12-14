namespace KdSoft.EtwEvents.EventSinks
{
    public class gRPCSinkOptions
    {
        public gRPCSinkOptions(
            string host,
            int? maxSendMessageSize = null,
            int? maxReceiveMessageSize = null,
            int? maxRetryAttempts = null,
            long? maxRetryBufferSize = null,
            long? maxRetryBufferPerCallSize = null
        ) {
            this.Host = host;
            this.MaxSendMessageSize = maxSendMessageSize;
            this.MaxReceiveMessageSize = maxReceiveMessageSize;
            this.MaxRetryAttempts = maxRetryAttempts;
            this.MaxRetryBufferSize = maxRetryBufferSize;
            this.MaxRetryBufferPerCallSize = maxRetryBufferPerCallSize;
        }

        public string Host { get; set; } = string.Empty;

        /// <summary>
        ///    Gets or sets the maximum message size in bytes that can be sent from the client.
        ///    Attempting to send a message that exceeds the configured maximum message size
        ///    results in an exception.
        ///    A null value removes the maximum message size limit. Defaults to null.
        /// </summary>
        public int? MaxSendMessageSize { get; set; }

        /// <summary>
        ///    Gets or sets the maximum message size in bytes that can be received by the client.
        ///    If the client receives a message that exceeds this limit, it throws an exception.
        ///    A null value removes the maximum message size limit. Defaults to 4,194,304 (4 MB).
        /// </summary>
        public int? MaxReceiveMessageSize { get; set; }

        /// <summary>
        ///    Gets or sets the maximum retry attempts. This value limits any retry and hedging
        ///    attempt values specified in the service config.
        ///    Setting this value alone doesn't enable retries. Retries are enabled in the service
        ///    config, which can be done using Grpc.Net.Client.GrpcChannelOptions.ServiceConfig.
        ///    A null value removes the maximum retry attempts limit. Defaults to 5.
        ///    Note: Experimental API that can change or be removed without any prior notice.
        /// </summary>
        public int? MaxRetryAttempts { get; set; }

        /// <summary>
        ///    Gets or sets the maximum buffer size in bytes that can be used to store sent
        ///    messages when retrying or hedging calls. If the buffer limit is exceeded, then
        ///    no more retry attempts are made and all hedging calls but one will be canceled.
        ///    This limit is applied across all calls made using the channel.
        ///    Setting this value alone doesn't enable retries. Retries are enabled in the service
        ///    config, which can be done using Grpc.Net.Client.GrpcChannelOptions.ServiceConfig.
        ///    A null value removes the maximum retry buffer size limit. Defaults to 16,777,216
        ///    (16 MB).
        ///    Note: Experimental API that can change or be removed without any prior notice.
        /// </summary>
        public long? MaxRetryBufferSize { get; set; }

        /// <summary>
        ///    Gets or sets the maximum buffer size in bytes that can be used to store sent
        ///    messages when retrying or hedging calls. If the buffer limit is exceeded, then
        ///    no more retry attempts are made and all hedging calls but one will be canceled.
        ///    This limit is applied to one call.
        ///    Setting this value alone doesn't enable retries. Retries are enabled in the service
        ///    config, which can be done using Grpc.Net.Client.GrpcChannelOptions.ServiceConfig.
        ///    A null value removes the maximum retry buffer size limit per call. Defaults to
        ///    1,048,576 (1 MB).
        ///    Note: Experimental API that can change or be removed without any prior notice.
        /// </summary>
        public long? MaxRetryBufferPerCallSize { get; set; }
    }
}
