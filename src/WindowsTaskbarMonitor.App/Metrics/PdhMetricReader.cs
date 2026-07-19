using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using WindowsTaskbarMonitor.Core;

namespace WindowsTaskbarMonitor.App.Metrics;

internal sealed class PdhMetricReader : IDisposable
{
    private const uint PdhFormatDouble = 0x00000200;
    private const uint PdhMoreData = 0x800007D2;
    private static readonly Regex GpuEnginePattern = new(
        @"luid_(?<luid>.+?)_phys_(?<physical>\d+)_eng_(?<engine>\d+)_engtype_",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private IntPtr _query;
    private IntPtr _diskRead;
    private IntPtr _diskWrite;
    private IntPtr _gpuUtilization;

    public PdhMetricReader()
    {
        if (PdhOpenQueryW(null, 0, out _query) != 0)
        {
            _query = IntPtr.Zero;
            return;
        }

        TryAddCounter(@"\PhysicalDisk(_Total)\Disk Read Bytes/sec", out _diskRead);
        TryAddCounter(@"\PhysicalDisk(_Total)\Disk Write Bytes/sec", out _diskWrite);
        TryAddCounter(@"\GPU Engine(*)\Utilization Percentage", out _gpuUtilization);
        PdhCollectQueryData(_query);
    }

    public PdhSample Read()
    {
        if (_query == IntPtr.Zero || PdhCollectQueryData(_query) != 0)
        {
            return default;
        }

        return new PdhSample(
            ReadDouble(_diskRead),
            ReadDouble(_diskWrite),
            ReadGpuUtilization());
    }

    public void Dispose()
    {
        if (_query != IntPtr.Zero)
        {
            PdhCloseQuery(_query);
            _query = IntPtr.Zero;
        }
    }

    private void TryAddCounter(string path, out IntPtr counter)
    {
        counter = IntPtr.Zero;
        if (_query != IntPtr.Zero && PdhAddEnglishCounterW(_query, path, 0, out var handle) == 0)
        {
            counter = handle;
        }
    }

    private static double ReadDouble(IntPtr counter)
    {
        if (counter == IntPtr.Zero ||
            PdhGetFormattedCounterValue(counter, PdhFormatDouble, out _, out var value) != 0 ||
            value.Status > 1)
        {
            return 0;
        }

        return MetricMath.NonNegative(value.DoubleValue);
    }

    private double? ReadGpuUtilization()
    {
        if (_gpuUtilization == IntPtr.Zero)
        {
            return null;
        }

        uint bufferSize = 0;
        var status = PdhGetFormattedCounterArrayW(
            _gpuUtilization,
            PdhFormatDouble,
            ref bufferSize,
            out var itemCount,
            IntPtr.Zero);

        if (status != PdhMoreData || bufferSize == 0 || itemCount == 0)
        {
            return null;
        }

        var buffer = Marshal.AllocHGlobal((int)bufferSize);
        try
        {
            status = PdhGetFormattedCounterArrayW(
                _gpuUtilization,
                PdhFormatDouble,
                ref bufferSize,
                out itemCount,
                buffer);

            if (status != 0)
            {
                return null;
            }

            var engines = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            var itemSize = Marshal.SizeOf<PdhFormattedCounterValueItem>();

            for (var index = 0; index < itemCount; index++)
            {
                var itemPointer = IntPtr.Add(buffer, checked((int)index * itemSize));
                var item = Marshal.PtrToStructure<PdhFormattedCounterValueItem>(itemPointer);
                if (item.Value.Status > 1 || item.Name == IntPtr.Zero)
                {
                    continue;
                }

                var name = Marshal.PtrToStringUni(item.Name) ?? string.Empty;
                var match = GpuEnginePattern.Match(name);
                if (!match.Success)
                {
                    continue;
                }

                var key = $"{match.Groups["luid"].Value}:{match.Groups["physical"].Value}:{match.Groups["engine"].Value}";
                engines[key] = engines.GetValueOrDefault(key) + MetricMath.NonNegative(item.Value.DoubleValue);
            }

            return engines.Count == 0 ? null : MetricMath.ClampPercent(engines.Values.Max());
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhOpenQueryW(string? dataSource, nuint userData, out IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhAddEnglishCounterW(IntPtr query, string fullCounterPath, nuint userData, out IntPtr counter);

    [DllImport("pdh.dll")]
    private static extern uint PdhCollectQueryData(IntPtr query);

    [DllImport("pdh.dll")]
    private static extern uint PdhGetFormattedCounterValue(
        IntPtr counter,
        uint format,
        out uint counterType,
        out PdhFormattedCounterValue value);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhGetFormattedCounterArrayW(
        IntPtr counter,
        uint format,
        ref uint bufferSize,
        out uint itemCount,
        IntPtr itemBuffer);

    [DllImport("pdh.dll")]
    private static extern uint PdhCloseQuery(IntPtr query);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct PdhFormattedCounterValue
    {
        public readonly uint Status;
        private readonly uint _padding;
        public readonly double DoubleValue;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct PdhFormattedCounterValueItem
    {
        public readonly IntPtr Name;
        public readonly PdhFormattedCounterValue Value;
    }
}

internal readonly record struct PdhSample(
    double DiskReadBytesPerSecond,
    double DiskWriteBytesPerSecond,
    double? GpuUsagePercent);
