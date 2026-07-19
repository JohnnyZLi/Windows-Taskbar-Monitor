using WindowsTaskbarMonitor.Core;

namespace WindowsTaskbarMonitor.App.Metrics;

internal sealed class SystemMetricCollector : IDisposable
{
    private readonly CpuUsageReader _cpu = new();
    private readonly NetworkRateReader _network = new();
    private readonly PdhMetricReader _pdh = new();
    private readonly HardwareSensorReader _hardware = new();

    public MetricSnapshot Sample(DateTimeOffset timestamp)
    {
        var memory = MemoryReader.Read();
        var network = _network.Read(timestamp);
        var performance = _pdh.Read();
        var hardware = _hardware.Read();

        return new MetricSnapshot(
            timestamp,
            MetricMath.ClampPercent(_cpu.Read()),
            hardware.CpuTemperatureCelsius,
            hardware.GpuUsagePercent ?? performance.GpuUsagePercent,
            hardware.GpuTemperatureCelsius,
            memory.UsedBytes,
            memory.TotalBytes,
            MetricMath.NonNegative(performance.DiskReadBytesPerSecond),
            MetricMath.NonNegative(performance.DiskWriteBytesPerSecond),
            MetricMath.NonNegative(network.DownloadBytesPerSecond),
            MetricMath.NonNegative(network.UploadBytesPerSecond));
    }

    public void Dispose()
    {
        _pdh.Dispose();
        _hardware.Dispose();
    }
}
