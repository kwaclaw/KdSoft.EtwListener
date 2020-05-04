﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using KdSoft.EtwLogging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace KdSoft.EtwEvents.WebClient.EventSinks
{
    class MongoSink: IEventSink
    {
        readonly MongoClient _client;
        readonly MongoSinkOptions _sinkInfo;
        readonly FilterDefinitionBuilder<BsonDocument> _fb;
        readonly List<WriteModel<BsonDocument>> _evl;

        IMongoDatabase? _db;
        IMongoCollection<BsonDocument>? _coll;
        CancellationToken _cancelToken;

        public string Name { get; }

        public Task<bool> RunTask { get; } = Task.FromResult(true);

        public MongoSink(string name, MongoSinkOptions sinkInfo, string dbUser, string dbPwd, CancellationToken cancelToken) {
            this.Name = name;

            _fb = new FilterDefinitionBuilder<BsonDocument>();
            _evl = new List<WriteModel<BsonDocument>>();

            _sinkInfo = sinkInfo;

            try {
                var mcs = new MongoClientSettings();
                mcs.Server = new MongoServerAddress(sinkInfo.Host, sinkInfo.Port);
                mcs.UseTls = true;
                mcs.Credential = MongoCredential.CreateCredential(sinkInfo.Database, dbUser, dbPwd);

                _client = new MongoClient(mcs);

                Initialize(cancelToken);
            }
            catch (Exception ex) {
                var errStr = $@"Error in {nameof(MongoSink)} initialization encountered:{Environment.NewLine}{ex.Message}";
                //healthReporter.ReportProblem(errStr, EventFlowContextIdentifiers.Configuration);
                throw;
            }
        }

        void Initialize(CancellationToken cancelToken) {
            this._cancelToken = cancelToken;
            try {
                _db = _client.GetDatabase(_sinkInfo.Database);
                _coll = _db.GetCollection<BsonDocument>(_sinkInfo.Collection);
            }
            catch (Exception ex) {
                var errStr = $@"Error in {nameof(MongoSink)} initialization encountered:{Environment.NewLine}{ex.Message}";
                //healthReporter.ReportProblem(errStr, EventFlowContextIdentifiers.Configuration);
                throw;
            }
        }

        WriteModel<BsonDocument> FromEvent(EtwEvent evt, long sequenceNo) {
            var filter = _fb.Empty;

            var efs = _sinkInfo.EventFilterFields;
            foreach (var ef in efs) {
                switch (ef) {
                    case "Timestamp":
                        filter = filter & _fb.Eq(ef, evt.TimeStamp);
                        break;
                    case "ProviderName":
                        filter = filter & _fb.Eq(ef, evt.ProviderName);
                        break;
                    case "Channel":
                        filter = filter & _fb.Eq(ef, evt.Channel);
                        break;
                    case "Id":
                        filter = filter & _fb.Eq(ef, evt.Id);
                        break;
                    case "Keywords":
                        filter = filter & _fb.Eq(ef, evt.Keywords);
                        break;
                    case "Level":
                        filter = filter & _fb.Eq(ef, evt.Level);
                        break;
                    case "Opcode":
                        filter = filter & _fb.Eq(ef, evt.Opcode);
                        break;
                    case "OpcodeName":
                        filter = filter & _fb.Eq(ef, evt.OpcodeName);
                        break;
                    case "TaskName":
                        filter = filter & _fb.Eq(ef, evt.TaskName);
                        break;
                    case "Version":
                        filter = filter & _fb.Eq(ef, evt.Version);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(string.Format("Event filter field not allowed: {0}", ef));
                }
            }

            var pfs = _sinkInfo.PayloadFilterFields;
            foreach (var pf in pfs) {
                evt.Payload.TryGetValue(pf, out string? payloadValue);
                filter = filter & _fb.Eq($"Payload.{pf}", payloadValue);
            }

            var payloadDoc = new BsonDocument();
            foreach (var payload in evt.Payload) {
                if (payload.Value == null)
                    payloadDoc.Add(payload.Key, BsonNull.Value);
                else
                    payloadDoc.Add(payload.Key, BsonValue.Create(payload.Value));
            }

            var replacement = new BsonDocument()
                .Add("SequenceNo", BsonValue.Create(sequenceNo))
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
            _cancelToken.ThrowIfCancellationRequested();
            var bwResult = await _coll!.BulkWriteAsync(_evl, new BulkWriteOptions { IsOrdered = false }).ConfigureAwait(false);
            _evl.Clear();
            //return bwResult;
            return true;
        }

        //TODO maybe use Interlocked and two lists to keep queueing while a bulk write is in process
        public ValueTask<bool> WriteAsync(EtwEvent evt, long sequenceNo) {
            _cancelToken.ThrowIfCancellationRequested();
            var writeModel = FromEvent(evt, sequenceNo);
            _evl.Add(writeModel);
            return new ValueTask<bool>(true);
        }

        //TODO catch exceptions here
        public ValueTask<bool> FlushAsync() {
            if (_evl.Count == 0)
                return new ValueTask<bool>(true);
            return new ValueTask<bool>(FlushAsyncInternal());
        }

        // Warning: ValueTasks should not be awaited multiple times
        public ValueTask DisposeAsync() {
            return default;
        }

        public void Dispose() {
            //
        }

        public bool Equals([AllowNull] IEventSink other) {
            if (object.ReferenceEquals(this, other))
                return true;
            if (other == null)
                return false;
            return StringComparer.Ordinal.Equals(this.Name, other.Name);
        }
    }
}