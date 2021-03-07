using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KdSoft.EtwEvents.Client.Shared;
using KdSoft.EtwLogging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace KdSoft.EtwEvents.EventSinks
{
    public class MongoSink: IEventSink
    {
        readonly IMongoCollection<BsonDocument> _coll;
        readonly IImmutableList<string> _eventFilterFields;
        readonly IImmutableList<string> _payloadFilterFields;
        readonly FilterDefinitionBuilder<BsonDocument> _fb;
        readonly List<WriteModel<BsonDocument>> _evl;
        readonly TaskCompletionSource<bool> _tcs;

        int _isDisposed = 0;

        public string Name { get; }

        public Task<bool> RunTask { get; }

        public MongoSink(
            string name,
            IMongoCollection<BsonDocument> coll,
            IImmutableList<string> eventFilterFields,
            IImmutableList<string> payloadFilterFields
        ) {
            this.Name = name;
            this._coll = coll;
            this._eventFilterFields = eventFilterFields;
            this._payloadFilterFields = payloadFilterFields;

            _tcs = new TaskCompletionSource<bool>();
            RunTask = _tcs.Task;

            _fb = new FilterDefinitionBuilder<BsonDocument>();
            _evl = new List<WriteModel<BsonDocument>>();
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
                _tcs.TrySetResult(true);
            }
        }

        // Warning: ValueTasks should not be awaited multiple times
        public ValueTask DisposeAsync() {
            Dispose();
            return ValueTask.CompletedTask;
        }

        public bool Equals([AllowNull] IEventSink other) {
            if (object.ReferenceEquals(this, other))
                return true;
            if (other == null)
                return false;
            return StringComparer.Ordinal.Equals(this.Name, other.Name);
        }

        WriteModel<BsonDocument> FromEvent(EtwEvent evt, long sequenceNo) {
            var filter = _fb.Empty;

            var efs = _eventFilterFields;
            foreach (var ef in efs) {
                filter &= ef switch {
                    "Timestamp" => _fb.Eq(ef, evt.TimeStamp),
                    "ProviderName" => _fb.Eq(ef, evt.ProviderName),
                    "Channel" => filter &= _fb.Eq(ef, evt.Channel),
                    "Id" => filter &= _fb.Eq(ef, evt.Id),
                    "Keywords" => _fb.Eq(ef, evt.Keywords),
                    "Level" => _fb.Eq(ef, evt.Level),
                    "Opcode" => _fb.Eq(ef, evt.Opcode),
                    "OpcodeName" => _fb.Eq(ef, evt.OpcodeName),
                    "TaskName" => _fb.Eq(ef, evt.TaskName),
                    "Version" => _fb.Eq(ef, evt.Version),
                    _ => throw new ArgumentOutOfRangeException($"Event filter field not allowed: {ef}"),
                };
            }

            var pfs = _payloadFilterFields;
            foreach (var pf in pfs) {
                evt.Payload.TryGetValue(pf, out string? payloadValue);
                filter &= _fb.Eq($"Payload.{pf}", payloadValue);
            }

            var payloadDoc = new BsonDocument();
            foreach (var payload in evt.Payload) {
                if (payload.Value == null)
                    payloadDoc.Add(payload.Key, BsonNull.Value);
                else
                    payloadDoc.Add(payload.Key, BsonValue.Create(payload.Value));
            }

            //TODO should we ignore sequenceNo?
            var replacement = new BsonDocument()
                //.Add("SequenceNo", BsonValue.Create(sequenceNo))
                .Add("Timestamp", new BsonDateTime(evt.TimeStamp.ToDateTime()))
                .Add("ProviderName", BsonValue.Create(evt.ProviderName))
                .Add("Channel", BsonValue.Create(evt.Channel))
                .Add("Id", BsonValue.Create(evt.Id))
                .Add("Keywords", BsonValue.Create(evt.Keywords))
                .Add("Level", BsonValue.Create(evt.Level))
                .Add("Opcode", BsonValue.Create(evt.Opcode))
                .Add("OpcodeName", BsonValue.Create(evt.OpcodeName))
                .Add("TaskName", BsonValue.Create(evt.TaskName))
                .Add("Version", BsonValue.Create(evt.Version))
                .Add(new BsonElement("Payload", payloadDoc));
            return new ReplaceOneModel<BsonDocument>(filter, replacement) { IsUpsert = true };
        }

        async Task<bool> FlushAsyncInternal() {
            var bwResult = await _coll.BulkWriteAsync(_evl, new BulkWriteOptions { IsOrdered = false }).ConfigureAwait(false);
            _evl.Clear();
            //return bwResult;
            return true;
        }

        public ValueTask<bool> FlushAsync() {
            if (IsDisposed)
                return new ValueTask<bool>(false);
            if (_evl.Count == 0)
                return new ValueTask<bool>(true);
            return new ValueTask<bool>(FlushAsyncInternal());
        }

        //TODO maybe use Interlocked and two lists to keep queueing while a bulk write is in process
        public ValueTask<bool> WriteAsync(EtwEvent evt, long sequenceNo) {
            if (IsDisposed)
                return new ValueTask<bool>(false);
            var writeModel = FromEvent(evt, sequenceNo);
            _evl.Add(writeModel);
            return new ValueTask<bool>(true);
        }

        public ValueTask<bool> WriteAsync(EtwEventBatch evtBatch, long sequenceNo) {
            if (IsDisposed)
                return new ValueTask<bool>(false);
            _evl.AddRange(evtBatch.Events.Select(evt => FromEvent(evt, sequenceNo++)));
            return new ValueTask<bool>(true);
        }
    }
}
