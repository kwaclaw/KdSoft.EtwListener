using OpenSearch.Net;

namespace KdSoft.EtwEvents.EventSinks
{
    [Serializable]
    public class OpenSearchSinkException: EventSinkException
    {
        public OpenSearchSinkException() : base() { }
        public OpenSearchSinkException(string message) : base(message) { }
        public OpenSearchSinkException(string message, Exception inner) : base(message, inner) { }
        public OpenSearchSinkException(string message, ServerError error) : base(message) {
            this.Error = error;
        }

        public ServerError? Error { get; private set; }

        public override string ToString() => base.ToString() + Environment.NewLine + Error?.ToString();
    }
}
