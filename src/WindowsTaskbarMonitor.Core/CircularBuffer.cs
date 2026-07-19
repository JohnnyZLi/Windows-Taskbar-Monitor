namespace WindowsTaskbarMonitor.Core;

public sealed class CircularBuffer<T>
{
    private readonly T[] _items;
    private int _next;
    private int _count;

    public CircularBuffer(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _items = new T[capacity];
    }

    public int Capacity => _items.Length;

    public int Count => _count;

    public void Add(T item)
    {
        _items[_next] = item;
        _next = (_next + 1) % Capacity;
        _count = Math.Min(_count + 1, Capacity);
    }

    public IReadOnlyList<T> Snapshot()
    {
        var result = new T[_count];
        var start = (_next - _count + Capacity) % Capacity;

        for (var index = 0; index < _count; index++)
        {
            result[index] = _items[(start + index) % Capacity];
        }

        return result;
    }

    public void Clear()
    {
        Array.Clear(_items);
        _next = 0;
        _count = 0;
    }
}
