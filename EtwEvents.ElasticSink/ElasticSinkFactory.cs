using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KdSoft.EtwEvents.Client.Shared;

namespace KdSoft.EtwEvents.EventSinks
{
    [EventSink(nameof(ElasticSink))]
    public class ElasticSinkFactory: IEventSinkFactory
    {
        static readonly JsonSerializerOptions _serializerOptions;

        static ElasticSinkFactory() {
            var evtSinkAssembly = Assembly.GetExecutingAssembly();
            var evtSinkDir = Path.GetDirectoryName(evtSinkAssembly.Location);
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) => Utils.DirectoryResolveAssembly(evtSinkDir!, args);

            _serializerOptions = new JsonSerializerOptions {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public Task<IEventSink> Create(string name, ElasticSinkOptions options, string user, string pwd) {
            var result = new ElasticSink(name, options, user, pwd, CancellationToken.None);
            return Task.FromResult((IEventSink)result);
        }

        public Task<IEventSink> Create(string name, string optionsJson, string credentialsJson) {
            var options = JsonSerializer.Deserialize<ElasticSinkOptions>(optionsJson, _serializerOptions);
            var creds = JsonSerializer.Deserialize<ElasticSinkCredentials>(credentialsJson, _serializerOptions);
            return Create(name, options!, creds!.User, creds!.Password);
        }

        public string GetCredentialsJsonSchema() {
            throw new NotImplementedException();
        }

        public string GetOptionsJsonSchema() {
            throw new NotImplementedException();
        }
    }
}
