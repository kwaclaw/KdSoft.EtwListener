using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using MongoDB.Driver;
using MongoDB.Driver.Core.Configuration;

namespace KdSoft.EtwEvents.EventSinks
{
    public class MongoSinkOptions
    {
        public MongoSinkOptions(IList<string> hosts, string replicaSet, string database, string collection, IEnumerable<string> eventFilterFields, IEnumerable<string> payloadFilterFields) : this() {
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

        public Uri GetConnectionStringUri(string user, string pwd) {
            var hostString = string.Join(',', Hosts);
            var replSetString = string.IsNullOrWhiteSpace(ReplicaSet) ? "" : $"?replicaSet={ReplicaSet}";
            var ub = new UriBuilder("mongodb", hostString) {
                UserName = user,
                Password = pwd,
                Path = Database,
                Query = replSetString,
            };
            return ub.Uri;
            //return $"mongodb://{user}:{pwd}@{hostString}/{Database}{replSetString}";
        }

        public MongoUrl GetConnectionUrl(string user, string pwd) {
            var mub = new MongoUrlBuilder {
                Scheme = ConnectionStringScheme.MongoDB,
                Servers = Hosts.Select(h => new MongoServerAddress(h)),
                ReplicaSetName = ReplicaSet,
                Username = user,
                Password = pwd,
                DatabaseName = Database,
            };
            return mub.ToMongoUrl();
        }

        public string GetHostParameter() {
            var hostString = string.Join(',', Hosts);
            var replSetString = string.IsNullOrWhiteSpace(ReplicaSet) ? "" : $"{ReplicaSet}/";
            return $"{replSetString}{hostString}";
        }
    }
}
