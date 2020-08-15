using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KdSoft.EtwEvents.Client.Shared;
using Newtonsoft.Json.Schema.Generation;

namespace KdSoft.EtwEvents.EventSinks
{
    [EventSink("MongoSink")]
    public class MongoSinkFactory: IEventSinkFactory
    {
        public Task<IEventSink> Create(string name, MongoSinkOptions options, string database, string dbUser, string dbPwd) {
            var result = new MongoSink(name, options, database, dbUser, dbPwd, CancellationToken.None);
            return Task.FromResult((IEventSink)result);
        }

        public Task<IEventSink> Create(string name, string optionsJson, string credentialsJson) {
            var options = JsonSerializer.Deserialize<MongoSinkOptions>(optionsJson);
            var creds = JsonSerializer.Deserialize<MongoSinkCredentials>(credentialsJson);
            return Create(name, options, creds.Database, creds.User, creds.Password);
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
