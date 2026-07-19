using System.Runtime.InteropServices;
using WindowsTaskbarMonitor.Core;

namespace WindowsTaskbarMonitor.App.Metrics;

internal sealed class CpuUsageReader
{
    private ulong? _previousIdle;
    private ulong? _previousKernel;
    private ulong? _previousUser;

    public double Read()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user))
        {
            return 0;
        }

        var idleValue = idle.ToUInt64();
        var kernelValue = kernel.ToUInt64();
        var userValue = user.ToUInt64();

        if (_previousIdle is null || _previousKernel is null || _previousUser is null)
        {
            Remember(idleValue, kernelValue, userValue);
            return 0;
        }

        var idleDelta = idleValue - _previousIdle.Value;
        var kernelDelta = kernelValue - _previousKernel.Value;
        var userDelta = userValue - _previousUser.Value;
        var total = kernelDelta + userDelta;

        Remember(idleValue, kernelValue, userValue);
        return total == 0 ? 0 : MetricMath.ClampPercent(100d * (total - idleDelta) / total);
    }

    private void Remember(ulong idle, ulong kernel, ulong user)
    {
        _previousIdle = idle;
        _previousKernel = kernel;
        _previousUser = user;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct FileTime
    {
        private readonly uint _low;
        private readonly uint _high;

        public ulong ToUInt64() => ((ulong)_high << 32) | _low;
    }
}
