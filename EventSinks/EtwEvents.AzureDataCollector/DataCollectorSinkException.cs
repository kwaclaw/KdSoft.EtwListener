using System;
using System.Net;

namespace KdSoft.EtwEvents.EventSinks
{
    [Serializable]
    public class DataCollectorSinkException: EventSinkException
    {
        public DataCollectorSinkException(HttpStatusCode statusCode) : base() {
            this.StatusCode = statusCode;
        }
        public DataCollectorSinkException(HttpStatusCode statusCode, string message) : base(message) {
            this.StatusCode = statusCode;
        }
        public DataCollectorSinkException(HttpStatusCode statusCode, string message, Exception inner) : base(message, inner) {
            this.StatusCode = statusCode;
        }
        public DataCollectorSinkException(HttpStatusCode statusCode, string message, string error) : base(message) {
            this.StatusCode = statusCode;
            this.Error = error;
        }

        public HttpStatusCode StatusCode { get; private set; }

        public string Error { get; private set; } = string.Empty;

        string ErrorSuffix => Error == string.Empty ? string.Empty : $":{Error}";

        public override string ToString() => base.ToString() + Environment.NewLine + $"{StatusCode}{ErrorSuffix}";
    }
}
