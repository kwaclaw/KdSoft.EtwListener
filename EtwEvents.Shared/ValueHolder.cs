namespace KdSoft.EtwEvents
{
    public class ValueHolder<T> where T : notnull, new()
    {
        public T Value { get; set; } = new T();

        public void Reset() {
            Value = new T();
        }
    }

    public class ValueHolder<T, V>
        where T : notnull, new()
        where V : notnull, new()
    {
        public T Value1 { get; set; } = new T();
        public V Value2 { get; set; } = new V();

        public void Reset() {
            Value1 = new T();
            Value2 = new V();
        }
    }

    public class ValueHolder<T, U, V>
        where T : notnull, new()
        where U : notnull, new()
        where V : notnull, new()
    {
        public T Value1 { get; set; } = new T();
        public U Value2 { get; set; } = new U();
        public V Value3 { get; set; } = new V();

        public void Reset() {
            Value1 = new T();
            Value2 = new U();
            Value3 = new V();
        }
    }
}
