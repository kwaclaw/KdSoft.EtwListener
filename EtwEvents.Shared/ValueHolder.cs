namespace KdSoft.EtwEvents
{
    public class ValueHolder<T> where T : notnull, new()
    {
        public T Value = new();

        public void Reset() {
            Value = new T();
        }
    }

    public class ValueHolder<T, V>
        where T : notnull, new()
        where V : notnull, new()
    {
        public T Value1 = new();
        public V Value2 = new();

        public void Reset() {
            Value1 = new();
            Value2 = new();
        }
    }

    public class ValueHolder<T, U, V>
        where T : notnull, new()
        where U : notnull, new()
        where V : notnull, new()
    {
        public T Value1 = new();
        public U Value2 = new();
        public V Value3 = new();

        public void Reset() {
            Value1 = new();
            Value2 = new();
            Value3 = new();
        }
    }
}
