namespace WindowsTaskbarMonitor.Core;

public static class MetricMath
{
    public static double ClampPercent(double value)
    {
        if (!double.IsFinite(value))
        {
            return 0;
        }

        return Math.Clamp(value, 0, 100);
    }

    public static double NonNegative(double value) =>
        double.IsFinite(value) ? Math.Max(0, value) : 0;
}
