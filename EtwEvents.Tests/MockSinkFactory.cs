using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using KdSoft.EtwEvents;
using Microsoft.Extensions.Logging;

namespace EtwEvents.Tests
{
    class MockSinkFactory: IEventSinkFactory
    {
        static readonly JsonSerializerOptions _serializerOptions;
        int _optionsCounter;

        static MockSinkFactory() {
            _serializerOptions = new JsonSerializerOptions {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public Task<IEventSink> Create(string optionsJson, string credentialsJson, ILogger logger) {
            var options = JsonSerializer.Deserialize<MockSinkOptions>(optionsJson, _serializerOptions);
            var creds = JsonSerializer.Deserialize<MockSinkCredentials>(credentialsJson, _serializerOptions);

            var cycleIndex = _optionsCounter++ % options.LifeCycles.Count;
            options.ActiveCycle = cycleIndex;

            var result = new MockSink(options);
            return Task.FromResult((IEventSink)result);
        }

        public string GetCredentialsJsonSchema() => throw new NotImplementedException();
        public string GetOptionsJsonSchema() => throw new NotImplementedException();
    }
}
