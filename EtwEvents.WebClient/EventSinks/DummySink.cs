using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KdSoft.EtwLogging;

namespace EtwEvents.WebClient.EventSinks
{
    sealed class DummySink: IEventSink
    {
        public DummySink(string name) {
            this.Name = name;
        }

        public string Name { get; }

        public Task<bool> RunTask => Task<bool>.FromResult(true);

        public void Dispose() {
            //
        }

        public ValueTask DisposeAsync() {
            return default;
        }

        public bool Equals([AllowNull] IEventSink other) {
            return string.Equals(this.Name, other?.Name, StringComparison.OrdinalIgnoreCase);
        }

        public ValueTask<bool> FlushAsync() {
            return new ValueTask<bool>(true);
        }

        public ValueTask<bool> WriteAsync(EtwEvent evt, long sequenceNo) {
            return new ValueTask<bool>(true);
        }
    }
}
