using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace KdSoft.EtwEvents.WebClient.EventSinks
{
    public class MongoSinkOptions
    {
        public MongoSinkOptions(IList<string> hosts, string replicaSet, string database, string collection, IEnumerable<string> eventFilterFields, IEnumerable<string> payloadFilterFields): this() {
            this.Hosts = hosts;
            this.ReplicaSet = replicaSet;
            this.Database = database;
            this.Collection = collection;
            this.EventFilterFields.AddRange(eventFilterFields);
            this.PayloadFilterFields.AddRange(payloadFilterFields);
        }

        public MongoSinkOptions() {
            this.EventFilterFields = ImmutableArray<string>.Empty;
            this.PayloadFilterFields = ImmutableArray<string>.Empty;
        }

        public IList<string> Hosts { get; set; } = Array.Empty<string>();

        public string ReplicaSet { get; set; } = string.Empty;

        public string Database { get; set; } = string.Empty;

        public string Collection { get; set; } = string.Empty;

        public ImmutableArray<string> EventFilterFields { get; set; }

        public ImmutableArray<string> PayloadFilterFields { get; set; }

        public string GetConnectionStringUri(string user, string pwd) {
            var hostString = string.Join(',', Hosts);
            var replSetString = string.IsNullOrWhiteSpace(ReplicaSet) ? "" : $"?replicaSet={ReplicaSet}";
            return $"mongodb://{user}:{pwd}@{hostString}/{Database}{replSetString}";
        }

        public string GetHostParameter() {
            var hostString = string.Join(',', Hosts);
            var replSetString = string.IsNullOrWhiteSpace(ReplicaSet) ? "" : $"{ReplicaSet}/";
            return $"{replSetString}{hostString}";
        }
    }
}
