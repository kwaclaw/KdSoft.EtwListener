using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KdSoft.EtwEvents;

namespace EtwEvents.Tests
{
    class MockSinkFactory: IEventSinkFactory
    {
        readonly List<MockSinkLifeCycle> _sinkLifeCycles = new List<MockSinkLifeCycle>();
        static readonly JsonSerializerOptions _serializerOptions;
        int _optionsCounter;

        static MockSinkFactory() {
            _serializerOptions = new JsonSerializerOptions {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public List<MockSinkLifeCycle> SinkLifeCycles => _sinkLifeCycles;

        public Task<IEventSink> Create(string optionsJson, string credentialsJson, IEventSinkContext context) {
            var options = JsonSerializer.Deserialize<MockSinkOptions>(optionsJson, _serializerOptions);
            var creds = JsonSerializer.Deserialize<MockSinkCredentials>(credentialsJson, _serializerOptions);

            var cycleIndex = _optionsCounter++ % options!.LifeCycles.Count;
            options.ActiveCycle = cycleIndex;

            // pass Event to next cycle, since two cycles could deal with the same event
            var previousCycle = _sinkLifeCycles.LastOrDefault();
            var previousEvent = previousCycle?.Event;
            var lifeCycle = new MockSinkLifeCycle { Event = previousEvent };
            _sinkLifeCycles.Add(lifeCycle);
            var result = new MockSink(options, lifeCycle);
            return Task.FromResult((IEventSink)result);
        }

        public string GetCredentialsJsonSchema() => throw new NotImplementedException();
        public string GetOptionsJsonSchema() => throw new NotImplementedException();
    }
}
