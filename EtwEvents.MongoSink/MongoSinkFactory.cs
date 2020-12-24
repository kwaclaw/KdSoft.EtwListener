using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KdSoft.EtwEvents.Client.Shared;
using Newtonsoft.Json.Schema.Generation;

namespace KdSoft.EtwEvents.EventSinks
{
    [EventSink(nameof(MongoSink))]
    public class MongoSinkFactory: IEventSinkFactory
    {
        static readonly JsonSerializerOptions _serializerOptions;

        static MongoSinkFactory() {
            ResolveEventHandler handler;

            var evtSinkAssembly = Assembly.GetExecutingAssembly();
            var evtSinkDir = Path.GetDirectoryName(evtSinkAssembly.Location);
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) => Utils.DirectoryResolveAssembly(evtSinkDir!, args);

            _serializerOptions = new JsonSerializerOptions {
                 PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public Task<IEventSink> Create(string name, MongoSinkOptions options, string database, string dbUser, string dbPwd) {
            var result = new MongoSink(name, options, database, dbUser, dbPwd, CancellationToken.None);
            return Task.FromResult((IEventSink)result);
        }

        public Task<IEventSink> Create(string name, string optionsJson, string credentialsJson) {
            var options = JsonSerializer.Deserialize<MongoSinkOptions>(optionsJson, _serializerOptions);
            var creds = JsonSerializer.Deserialize<MongoSinkCredentials>(credentialsJson, _serializerOptions);
            return Create(name, options!, creds!.Database, creds!.User, creds!.Password);
        }

        string GetJsonSchema<T>() {
            var generator = new JSchemaGenerator();
            var schema = generator.Generate(typeof(T));
            return schema.ToString();
        }

        public string GetCredentialsJsonSchema() {
            return GetJsonSchema<MongoSinkCredentials>();
        }

        public string GetOptionsJsonSchema() {
            return GetJsonSchema<MongoSinkOptions>();
        }
    }
}
