namespace WindowsTaskbarMonitor.Core;

public sealed record MetricSnapshot(
    DateTimeOffset CapturedAt,
    double CpuUsagePercent,
    double? CpuTemperatureCelsius,
    double? GpuUsagePercent,
    double? GpuTemperatureCelsius,
    ulong MemoryUsedBytes,
    ulong MemoryTotalBytes,
    double DiskReadBytesPerSecond,
    double DiskWriteBytesPerSecond,
    double NetworkDownloadBytesPerSecond,
    double NetworkUploadBytesPerSecond)
{
    public double MemoryUsagePercent => MemoryTotalBytes == 0
        ? 0
        : MetricMath.ClampPercent(100d * MemoryUsedBytes / MemoryTotalBytes);

    public double? GetTrayPercent(TrayMetric metric) => metric switch
    {
        TrayMetric.Cpu => CpuUsagePercent,
        TrayMetric.Gpu => GpuUsagePercent,
        TrayMetric.Memory => MemoryUsagePercent,
        _ => null
    };
}
