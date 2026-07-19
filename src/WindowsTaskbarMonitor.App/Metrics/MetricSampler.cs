using WindowsTaskbarMonitor.Core;

namespace WindowsTaskbarMonitor.App.Metrics;

internal sealed class MetricSampler : IDisposable
{
    private readonly SystemMetricCollector _collector;
    private readonly TimeSpan _interval;
    private readonly CancellationTokenSource _cancellation = new();
    private Task? _loop;

    public MetricSampler(SystemMetricCollector collector, TimeSpan interval)
    {
        _collector = collector;
        _interval = interval;
    }

    public event EventHandler<MetricSnapshot>? Sampled;

    public void Start() => _loop ??= Task.Run(SampleLoopAsync);

    public void Dispose()
    {
        _cancellation.Cancel();
        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException exception) when (exception.InnerExceptions.All(inner => inner is TaskCanceledException))
        {
        }

        _cancellation.Dispose();
        _collector.Dispose();
    }

    private async Task SampleLoopAsync()
    {
        using var timer = new PeriodicTimer(_interval);

        do
        {
            try
            {
                Sampled?.Invoke(this, _collector.Sample(DateTimeOffset.UtcNow));
            }
            catch
            {
                // One bad sensor read must not terminate a long-running tray process.
            }
        }
        while (await timer.WaitForNextTickAsync(_cancellation.Token).ConfigureAwait(false));
    }
}
