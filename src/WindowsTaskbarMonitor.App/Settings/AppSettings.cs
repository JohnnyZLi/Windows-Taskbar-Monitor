using WindowsTaskbarMonitor.Core;

namespace WindowsTaskbarMonitor.App.Settings;

internal sealed record AppSettings(TrayMetric TrayMetric = TrayMetric.Cpu);
