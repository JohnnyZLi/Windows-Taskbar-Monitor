using LibreHardwareMonitor.Hardware;
using WindowsTaskbarMonitor.Core;

namespace WindowsTaskbarMonitor.App.Metrics;

internal sealed class HardwareSensorReader : IDisposable
{
    private readonly Computer? _computer;

    public HardwareSensorReader()
    {
        try
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true
            };
            _computer.Open();
        }
        catch
        {
            _computer?.Close();
            _computer = null;
        }
    }

    public HardwareSample Read()
    {
        if (_computer is null)
        {
            return default;
        }

        try
        {
            foreach (var hardware in _computer.Hardware)
            {
                UpdateRecursive(hardware);
            }

            var cpuSensors = _computer.Hardware
                .Where(hardware => hardware.HardwareType == HardwareType.Cpu)
                .SelectMany(SensorsRecursive)
                .ToArray();

            var gpuSensors = _computer.Hardware
                .Where(IsGpu)
                .SelectMany(SensorsRecursive)
                .ToArray();

            return new HardwareSample(
                SelectTemperature(cpuSensors, ["CPU Package", "Core Average", "CPU (Tctl/Tdie)"]),
                SelectLoad(gpuSensors, ["GPU Core", "D3D 3D"]),
                SelectTemperature(gpuSensors, ["GPU Core", "GPU Hot Spot"]));
        }
        catch
        {
            return default;
        }
    }

    public void Dispose() => _computer?.Close();

    private static bool IsGpu(IHardware hardware) => hardware.HardwareType is
        HardwareType.GpuAmd or HardwareType.GpuIntel or HardwareType.GpuNvidia;

    private static void UpdateRecursive(IHardware hardware)
    {
        hardware.Update();
        foreach (var child in hardware.SubHardware)
        {
            UpdateRecursive(child);
        }
    }

    private static IEnumerable<ISensor> SensorsRecursive(IHardware hardware)
    {
        foreach (var sensor in hardware.Sensors)
        {
            yield return sensor;
        }

        foreach (var child in hardware.SubHardware)
        {
            foreach (var sensor in SensorsRecursive(child))
            {
                yield return sensor;
            }
        }
    }

    private static double? SelectTemperature(IEnumerable<ISensor> sensors, IReadOnlyList<string> preferredNames) =>
        SelectSensor(sensors, SensorType.Temperature, preferredNames, value => value is > -20 and < 150);

    private static double? SelectLoad(IEnumerable<ISensor> sensors, IReadOnlyList<string> preferredNames) =>
        SelectSensor(sensors, SensorType.Load, preferredNames, value => value is >= 0 and <= 100);

    private static double? SelectSensor(
        IEnumerable<ISensor> sensors,
        SensorType type,
        IReadOnlyList<string> preferredNames,
        Func<double, bool> valid)
    {
        var candidates = sensors
            .Where(sensor => sensor.SensorType == type && sensor.Value is not null)
            .Select(sensor => (sensor.Name, Value: (double)sensor.Value!.Value))
            .Where(sensor => valid(sensor.Value))
            .ToArray();

        foreach (var preferredName in preferredNames)
        {
            var preferred = candidates.FirstOrDefault(sensor =>
                sensor.Name.Contains(preferredName, StringComparison.OrdinalIgnoreCase));
            if (preferred != default)
            {
                return type == SensorType.Load ? MetricMath.ClampPercent(preferred.Value) : preferred.Value;
            }
        }

        if (candidates.Length == 0)
        {
            return null;
        }

        var value = candidates.Max(sensor => sensor.Value);
        return type == SensorType.Load ? MetricMath.ClampPercent(value) : value;
    }
}

internal readonly record struct HardwareSample(
    double? CpuTemperatureCelsius,
    double? GpuUsagePercent,
    double? GpuTemperatureCelsius);
