using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace KdSoft.EtwEvents.EventSinks
{
    [EventSink(nameof(OpenSearchSink))]
    public class OpenSearchSinkFactory: IEventSinkFactory
    {
        static readonly JsonSerializerOptions _serializerOptions;

        static OpenSearchSinkFactory() {
            _serializerOptions = new JsonSerializerOptions {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public Task<IEventSink> Create(string optionsJson, string credentialsJson, IEventSinkContext context) {
            var options = JsonSerializer.Deserialize<OpenSearchSinkOptions>(optionsJson, _serializerOptions);
            if (options == null)
                throw new ArgumentException("Missing ElasticSinkOptions", nameof(options));
            var creds = JsonSerializer.Deserialize<OpenSearchSinkCredentials>(credentialsJson, _serializerOptions);
            if (creds == null)
                throw new ArgumentException("Missing ElasticSinkCredentials", nameof(creds));
            var result = new OpenSearchSink(options, creds, context);
            return Task.FromResult((IEventSink)result);
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
    ""ApiKeyId"": {
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
