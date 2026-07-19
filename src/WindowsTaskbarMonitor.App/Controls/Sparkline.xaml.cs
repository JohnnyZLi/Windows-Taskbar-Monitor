using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace WindowsTaskbarMonitor.App.Controls;

public sealed partial class Sparkline : UserControl
{
    private IReadOnlyList<double> _values = Array.Empty<double>();

    public Sparkline()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Redraw();
    }

    public void SetValues(IReadOnlyList<double> values)
    {
        _values = values;
        Redraw();
    }

    private void Redraw()
    {
        PlotLine.Points.Clear();
        if (_values.Count < 2 || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var minimum = _values.Min();
        var maximum = _values.Max();
        var range = Math.Max(1, maximum - minimum);

        for (var index = 0; index < _values.Count; index++)
        {
            var x = index * ActualWidth / (_values.Count - 1);
            var normalized = (_values[index] - minimum) / range;
            var y = ActualHeight - (normalized * (ActualHeight - 4)) - 2;
            PlotLine.Points.Add(new Point(x, y));
        }
    }
}
