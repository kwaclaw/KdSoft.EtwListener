namespace KdSoft.EtwEvents.Tests
{
    class MockDisposable: IDisposable
    {
        public static MockDisposable Instance { get; } = new MockDisposable();

        public void Dispose() { }
    }
}
