namespace KdSoft.EtwEvents
{
    public class ValueHolder<T> where T: notnull, new()
    {
        public T Value { get; set; } = new T();
    }
}
