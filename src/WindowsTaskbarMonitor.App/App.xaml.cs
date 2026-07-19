using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using WindowsTaskbarMonitor.App.Metrics;
using WindowsTaskbarMonitor.App.Settings;
using WindowsTaskbarMonitor.App.Tray;
using WindowsTaskbarMonitor.Core;

namespace WindowsTaskbarMonitor.App;

public partial class App : Application
{
    private readonly Mutex _singleInstance;
    private readonly bool _ownsInstance;
    private DispatcherQueue? _dispatcher;
    private MainWindow? _window;
    private TrayIconService? _trayIcon;
    private MetricSampler? _sampler;
    private AppSettings _settings = new();
    private MetricSnapshot? _latestSnapshot;

    public App()
    {
        _singleInstance = new Mutex(
            initiallyOwned: true,
            "Local\\JohnnyLi.WindowsTaskbarMonitor",
            out _ownsInstance);
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (!_ownsInstance)
        {
            Exit();
            return;
        }

        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _settings = SettingsStore.Load();
        _window = new MainWindow(_settings.TrayMetric);
        _window.TrayMetricChanged += OnTrayMetricChanged;

        _trayIcon = new TrayIconService();
        _trayIcon.OpenRequested += OnTrayOpenRequested;
        _trayIcon.ExitRequested += OnExitRequested;
        _trayIcon.Start();

        _sampler = new MetricSampler(new SystemMetricCollector(), TimeSpan.FromSeconds(1));
        _sampler.Sampled += OnSampled;
        _sampler.Start();
    }

    private void OnSampled(object? sender, MetricSnapshot snapshot)
    {
        _dispatcher?.TryEnqueue(() =>
        {
            _latestSnapshot = snapshot;
            _window?.UpdateMetrics(snapshot);
            UpdateTrayIcon(snapshot);
        });
    }

    private void OnTrayMetricChanged(object? sender, TrayMetric metric)
    {
        _settings = _settings with { TrayMetric = metric };
        SettingsStore.Save(_settings);

        if (_latestSnapshot is not null)
        {
            UpdateTrayIcon(_latestSnapshot);
        }
    }

    private void OnTrayOpenRequested(object? sender, EventArgs args)
    {
        if (_window is null || _trayIcon is null)
        {
            return;
        }

        _window.ToggleNear(_trayIcon.GetBounds());
    }

    private void OnExitRequested(object? sender, EventArgs args) => Shutdown();

    private void UpdateTrayIcon(MetricSnapshot snapshot)
    {
        var value = snapshot.GetTrayPercent(_settings.TrayMetric);
        var label = value is null ? "--" : Math.Round(MetricMath.ClampPercent(value.Value)).ToString("0");
        var tooltip = $"CPU {MetricFormatter.Percent(snapshot.CpuUsagePercent)} · " +
                      $"GPU {MetricFormatter.Percent(snapshot.GpuUsagePercent)} · " +
                      $"RAM {MetricFormatter.Percent(snapshot.MemoryUsagePercent)}";
        _trayIcon?.Update(label, tooltip);
    }

    private void Shutdown()
    {
        _sampler?.Dispose();
        _sampler = null;
        _trayIcon?.Dispose();
        _trayIcon = null;
        _window?.Close();
        _window = null;

        if (_ownsInstance)
        {
            _singleInstance.ReleaseMutex();
        }

        _singleInstance.Dispose();
        Exit();
    }
}
