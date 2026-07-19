using WindowsTaskbarMonitor.Core;

namespace WindowsTaskbarMonitor.Core.Tests;

public sealed class CounterRateTests
{
    private static readonly DateTimeOffset Start = new(2026, 7, 19, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FirstObservationProducesZero()
    {
        var rate = new CounterRate();

        Assert.Equal(0, rate.Next(1_000, Start));
    }

    [Fact]
    public void CalculatesRateAcrossElapsedTime()
    {
        var rate = new CounterRate();
        rate.Next(1_000, Start);

        var result = rate.Next(3_000, Start.AddSeconds(2));

        Assert.Equal(1_000, result);
    }

    [Fact]
    public void CounterResetProducesZeroInsteadOfNegativeRate()
    {
        var rate = new CounterRate();
        rate.Next(5_000, Start);

        Assert.Equal(0, rate.Next(100, Start.AddSeconds(1)));
    }

    [Fact]
    public void NonIncreasingTimestampProducesZero()
    {
        var rate = new CounterRate();
        rate.Next(1_000, Start);

        Assert.Equal(0, rate.Next(2_000, Start));
    }
}
