namespace WindowsTaskbarMonitor.Core;

public sealed class CounterRate
{
    private ulong? _previousValue;
    private DateTimeOffset? _previousTimestamp;

    public double Next(ulong value, DateTimeOffset timestamp)
    {
        if (_previousValue is null || _previousTimestamp is null)
        {
            Remember(value, timestamp);
            return 0;
        }

        var elapsed = (timestamp - _previousTimestamp.Value).TotalSeconds;
        if (elapsed <= 0 || value < _previousValue.Value)
        {
            Remember(value, timestamp);
            return 0;
        }

        var rate = (value - _previousValue.Value) / elapsed;
        Remember(value, timestamp);
        return MetricMath.NonNegative(rate);
    }

    public void Reset()
    {
        _previousValue = null;
        _previousTimestamp = null;
    }

    private void Remember(ulong value, DateTimeOffset timestamp)
    {
        _previousValue = value;
        _previousTimestamp = timestamp;
    }
}
