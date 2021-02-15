using System.Collections.Generic;
using System.Collections.Immutable;
using MongoDB.Driver;

namespace KdSoft.EtwEvents.EventSinks
{
    public class MongoSinkOptions
    {
        public MongoSinkOptions(string origin, string replicaSet, string database, string collection, IEnumerable<string> eventFilterFields, IEnumerable<string> payloadFilterFields) : this() {
            this.Origin = origin;
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

        public string Origin { get; set; } = string.Empty;

        public string ReplicaSet { get; set; } = string.Empty;

        public string Database { get; set; } = string.Empty;

        public string Collection { get; set; } = string.Empty;

        /// <summary>
        /// Event fields on which to match the target records to do an Upsert (update when matching, insert otherwise)
        /// </summary>
        public ImmutableArray<string> EventFilterFields { get; set; }

        /// <summary>
        /// Event.Payload fields on which to match the target records to do an Upsert (update when matching, insert otherwise)
        /// </summary>
        public ImmutableArray<string> PayloadFilterFields { get; set; }

        public MongoUrl GetConnectionUrl(string user, string pwd) {
            var mub = new MongoUrlBuilder(Origin) {
                ReplicaSetName = ReplicaSet,
                Username = user,
                Password = pwd,
                DatabaseName = Database,
            };
            return mub.ToMongoUrl();
        }
    }
}
