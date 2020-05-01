using System.Collections.Generic;
using System.Collections.Immutable;

namespace KdSoft.EtwEvents.WebClient.EventSinks
{
    public class MongoSinkOptions
    {
        public MongoSinkOptions(string host, int port, string database, string collection, IEnumerable<string> eventFilterFields, IEnumerable<string> payloadFilterFields) {
            this.Host = host;
            this.Port = port;
            this.Database = database;
            this.Collection = collection;
            this.EventFilterFields = ImmutableArray<string>.Empty.AddRange(eventFilterFields);
            this.PayloadFilterFields = ImmutableArray<string>.Empty.AddRange(payloadFilterFields);
        }

        public string Host { get; }

        public int Port { get; }

        public string Database { get; }

        public string Collection { get; }

        public ImmutableArray<string> EventFilterFields { get; }

        public ImmutableArray<string> PayloadFilterFields { get; }
    }
}
