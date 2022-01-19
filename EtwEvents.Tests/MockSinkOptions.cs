using System;
using System.Collections.Generic;

namespace EtwEvents.Tests
{
    class MockSinkOptions
    {
        public IList<(int maxWrites, TimeSpan duration)> LifeCycles { get; set; } = new List<(int maxWrites, TimeSpan duration)>();
        public int ActiveCycle { get; set; }
    }
}
