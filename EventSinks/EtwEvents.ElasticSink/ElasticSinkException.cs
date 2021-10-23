using System;
using Elasticsearch.Net;
using KdSoft.EtwEvents.Client.Shared;

namespace KdSoft.EtwEvents.EventSinks
{
    [Serializable]
    public class ElasticSinkException: EventSinkException
    {
        public ElasticSinkException() : base() { }
        public ElasticSinkException(string message) : base(message) { }
        public ElasticSinkException(string message, Exception inner) : base(message, inner) { }
        public ElasticSinkException(string message, ServerError error) : base(message) {
            this.Error = error;
        }

        public ServerError? Error { get; private set; }

        public override string ToString() => base.ToString() + Environment.NewLine + Error?.ToString();
    }
}
