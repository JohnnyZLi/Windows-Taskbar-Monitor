using System.Globalization;

namespace WindowsTaskbarMonitor.Core;

public static class MetricFormatter
{
    private static readonly string[] BinaryUnits = ["B", "KiB", "MiB", "GiB", "TiB"];

    public static string Percent(double? value) => value is null
        ? "—"
        : $"{MetricMath.ClampPercent(value.Value).ToString("0", CultureInfo.InvariantCulture)}%";

    public static string Temperature(double? celsius) => celsius is null || !double.IsFinite(celsius.Value)
        ? "Unavailable"
        : $"{celsius.Value.ToString("0", CultureInfo.InvariantCulture)} °C";

    public static string Bytes(ulong bytes) => FormatBinary(bytes);

    public static string Rate(double bytesPerSecond) =>
        $"{FormatBinary((ulong)Math.Round(MetricMath.NonNegative(bytesPerSecond)))}/s";

    private static string FormatBinary(ulong bytes)
    {
        var value = (double)bytes;
        var unit = 0;

        while (value >= 1024 && unit < BinaryUnits.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        var format = value >= 100 || unit == 0 ? "0" : value >= 10 ? "0.0" : "0.00";
        return $"{value.ToString(format, CultureInfo.InvariantCulture)} {BinaryUnits[unit]}";
    }
}
