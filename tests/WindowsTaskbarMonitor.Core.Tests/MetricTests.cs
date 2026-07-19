using WindowsTaskbarMonitor.Core;

namespace WindowsTaskbarMonitor.Core.Tests;

public sealed class MetricTests
{
    [Theory]
    [InlineData(-10, 0)]
    [InlineData(42.5, 42.5)]
    [InlineData(120, 100)]
    public void ClampPercentBoundsValues(double input, double expected)
    {
        Assert.Equal(expected, MetricMath.ClampPercent(input));
    }

    [Fact]
    public void SnapshotCalculatesMemoryPercentage()
    {
        var snapshot = CreateSnapshot(memoryUsed: 12, memoryTotal: 16);

        Assert.Equal(75, snapshot.MemoryUsagePercent);
        Assert.Equal(75, snapshot.GetTrayPercent(TrayMetric.Memory));
    }

    [Fact]
    public void SnapshotHandlesUnknownMemoryCapacity()
    {
        var snapshot = CreateSnapshot(memoryUsed: 12, memoryTotal: 0);

        Assert.Equal(0, snapshot.MemoryUsagePercent);
    }

    [Theory]
    [InlineData(0, "0 B/s")]
    [InlineData(1024, "1.00 KiB/s")]
    [InlineData(1_048_576, "1.00 MiB/s")]
    public void RateUsesExplicitBinaryUnits(double value, string expected)
    {
        Assert.Equal(expected, MetricFormatter.Rate(value));
    }

    [Fact]
    public void MissingTemperatureIsExplicit()
    {
        Assert.Equal("Unavailable", MetricFormatter.Temperature(null));
    }

    private static MetricSnapshot CreateSnapshot(ulong memoryUsed, ulong memoryTotal) => new(
        DateTimeOffset.UnixEpoch,
        CpuUsagePercent: 20,
        CpuTemperatureCelsius: 50,
        GpuUsagePercent: 30,
        GpuTemperatureCelsius: 60,
        MemoryUsedBytes: memoryUsed,
        MemoryTotalBytes: memoryTotal,
        DiskReadBytesPerSecond: 0,
        DiskWriteBytesPerSecond: 0,
        NetworkDownloadBytesPerSecond: 0,
        NetworkUploadBytesPerSecond: 0);
}
