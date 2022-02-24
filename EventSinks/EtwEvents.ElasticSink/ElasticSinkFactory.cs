using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace KdSoft.EtwEvents.EventSinks
{
    [EventSink(nameof(ElasticSink))]
    public class ElasticSinkFactory: IEventSinkFactory
    {
        static readonly JsonSerializerOptions _serializerOptions;

        static ElasticSinkFactory() {
            _serializerOptions = new JsonSerializerOptions {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public Task<IEventSink> Create(ElasticSinkOptions options, string user, string pwd, ILogger logger) {
            var result = new ElasticSink(options, user, pwd, logger);
            return Task.FromResult((IEventSink)result);
        }

        public Task<IEventSink> Create(string optionsJson, string credentialsJson, ILogger logger) {
            var options = JsonSerializer.Deserialize<ElasticSinkOptions>(optionsJson, _serializerOptions);
            var creds = JsonSerializer.Deserialize<ElasticSinkCredentials>(credentialsJson, _serializerOptions);
            return Create(options!, creds!.User, creds!.Password, logger);
        }

        public string GetCredentialsJsonSchema() {
            return @"
{
  ""type"": ""object"",
  ""properties"": {
                ""User"": {
                    ""type"": [
                      ""string"",
        ""null""
                  ]
    },
    ""Password"": {
                    ""type"": [
                      ""string"",
        ""null""
      ]
    },
    ""ApiKey"": {
                    ""type"": [
                      ""string"",
        ""null""
      ]
    },
    ""SubjectCN"": {
                    ""type"": [
                      ""string"",
        ""null""
      ]
    }
            },
  ""required"": [
    ""User"",
    ""Password"",
    ""ApiKey"",
    ""SubjectCN""
  ]
}";
        }

        public string GetOptionsJsonSchema() {
            return @"
{
  ""type"": ""object"",
  ""properties"": {
                ""Nodes"": {
                    ""type"": [
                      ""array"",
        ""null""
                  ],
      ""items"": {
                        ""type"": [
                          ""string"",
          ""null""
        ]
      }
                },
    ""IndexFormat"": {
                    ""type"": [
                      ""string"",
        ""null""
      ]
    }
            },
  ""required"": [
    ""Nodes"",
    ""IndexFormat""
  ]
}
";
        }
    }
}
