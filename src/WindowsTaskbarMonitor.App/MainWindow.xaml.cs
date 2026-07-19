using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.System;
using WindowsTaskbarMonitor.App.Tray;
using WindowsTaskbarMonitor.Core;

namespace WindowsTaskbarMonitor.App;

public sealed partial class MainWindow : Window
{
    private const int FlyoutWidth = 432;
    private const int FlyoutHeight = 720;
    private readonly CircularBuffer<double> _cpuHistory = new(60);
    private readonly CircularBuffer<double> _gpuHistory = new(60);
    private readonly CircularBuffer<double> _memoryHistory = new(60);
    private readonly CircularBuffer<double> _diskHistory = new(60);
    private readonly CircularBuffer<double> _networkHistory = new(60);
    private bool _isVisible;
    private bool _isSettingSelection;

    public MainWindow(TrayMetric initialTrayMetric)
    {
        InitializeComponent();
        SystemBackdrop = new DesktopAcrylicBackdrop();
        ConfigureWindow();
        SetTrayMetric(initialTrayMetric);

        Activated += OnWindowActivated;
        RootSurface.KeyDown += OnRootKeyDown;
    }

    public event EventHandler<TrayMetric>? TrayMetricChanged;

    public void UpdateMetrics(MetricSnapshot snapshot)
    {
        CpuUsageText.Text = MetricFormatter.Percent(snapshot.CpuUsagePercent);
        CpuTemperatureText.Text = MetricFormatter.Temperature(snapshot.CpuTemperatureCelsius);
        GpuUsageText.Text = MetricFormatter.Percent(snapshot.GpuUsagePercent);
        GpuTemperatureText.Text = MetricFormatter.Temperature(snapshot.GpuTemperatureCelsius);
        MemoryUsageText.Text = MetricFormatter.Percent(snapshot.MemoryUsagePercent);
        MemoryDetailText.Text = $"{MetricFormatter.Bytes(snapshot.MemoryUsedBytes)} of {MetricFormatter.Bytes(snapshot.MemoryTotalBytes)}";
        DiskReadText.Text = MetricFormatter.Rate(snapshot.DiskReadBytesPerSecond);
        DiskWriteText.Text = MetricFormatter.Rate(snapshot.DiskWriteBytesPerSecond);
        NetworkDownText.Text = MetricFormatter.Rate(snapshot.NetworkDownloadBytesPerSecond);
        NetworkUpText.Text = MetricFormatter.Rate(snapshot.NetworkUploadBytesPerSecond);
        UpdatedText.Text = $"Updated {snapshot.CapturedAt.ToLocalTime():HH:mm:ss}";

        _cpuHistory.Add(snapshot.CpuUsagePercent);
        _gpuHistory.Add(snapshot.GpuUsagePercent ?? 0);
        _memoryHistory.Add(snapshot.MemoryUsagePercent);
        _diskHistory.Add(snapshot.DiskReadBytesPerSecond + snapshot.DiskWriteBytesPerSecond);
        _networkHistory.Add(snapshot.NetworkDownloadBytesPerSecond + snapshot.NetworkUploadBytesPerSecond);

        CpuSparkline.SetValues(_cpuHistory.Snapshot());
        GpuSparkline.SetValues(_gpuHistory.Snapshot());
        MemorySparkline.SetValues(_memoryHistory.Snapshot());
        DiskSparkline.SetValues(_diskHistory.Snapshot());
        NetworkSparkline.SetValues(_networkHistory.Snapshot());
    }

    public void ToggleNear(TrayBounds bounds)
    {
        if (_isVisible)
        {
            Hide();
            return;
        }

        var displayArea = DisplayArea.GetFromPoint(
            new PointInt32(bounds.Right, bounds.Bottom),
            DisplayAreaFallback.Primary);
        var workArea = displayArea.WorkArea;
        var x = Math.Clamp(
            bounds.Right - FlyoutWidth,
            workArea.X + 8,
            workArea.X + workArea.Width - FlyoutWidth - 8);
        var y = Math.Clamp(
            bounds.Top - FlyoutHeight - 8,
            workArea.Y + 8,
            workArea.Y + workArea.Height - FlyoutHeight - 8);

        AppWindow.MoveAndResize(new RectInt32(x, y, FlyoutWidth, FlyoutHeight));
        Activate();
        _isVisible = true;
    }

    private void ConfigureWindow()
    {
        AppWindow.Title = "Taskbar Monitor";
        AppWindow.Resize(new SizeInt32(FlyoutWidth, FlyoutHeight));

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsResizable = false;
            presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
        }
    }

    private void SetTrayMetric(TrayMetric metric)
    {
        _isSettingSelection = true;
        TrayMetricSelector.SelectedIndex = metric switch
        {
            TrayMetric.Cpu => 0,
            TrayMetric.Gpu => 1,
            TrayMetric.Memory => 2,
            _ => 0
        };
        _isSettingSelection = false;
    }

    private void OnTrayMetricSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (_isSettingSelection || TrayMetricSelector.SelectedIndex < 0)
        {
            return;
        }

        var metric = TrayMetricSelector.SelectedIndex switch
        {
            1 => TrayMetric.Gpu,
            2 => TrayMetric.Memory,
            _ => TrayMetric.Cpu
        };
        TrayMetricChanged?.Invoke(this, metric);
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        if (_isVisible && args.WindowActivationState == WindowActivationState.Deactivated)
        {
            Hide();
        }
    }

    private void OnRootKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == VirtualKey.Escape)
        {
            Hide();
            args.Handled = true;
        }
    }

    private void Hide()
    {
        AppWindow.Hide();
        _isVisible = false;
    }
}
