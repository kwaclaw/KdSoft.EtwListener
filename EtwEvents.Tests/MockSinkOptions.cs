namespace KdSoft.EtwEvents.Tests
{
    class MockSinkOptions
    {
        public IList<Tuple<int, TimeSpan>> LifeCycles { get; set; } = new List<Tuple<int, TimeSpan>>();
        public int ActiveCycle { get; set; }
    }
}
