using System;
using System.Collections.Generic;

namespace EtwEvents.Tests
{
    class MockSinkOptions
    {
        public IList<Tuple<int, TimeSpan>> LifeCycles { get; set; } = new List<Tuple<int, TimeSpan>>();
        public int ActiveCycle { get; set; }
    }
}
