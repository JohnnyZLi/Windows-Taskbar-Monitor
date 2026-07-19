using WindowsTaskbarMonitor.Core;

namespace WindowsTaskbarMonitor.Core.Tests;

public sealed class CircularBufferTests
{
    [Fact]
    public void RejectsInvalidCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CircularBuffer<int>(0));
    }

    [Fact]
    public void ReturnsItemsInInsertionOrderBeforeWrapping()
    {
        var buffer = new CircularBuffer<int>(3);

        buffer.Add(10);
        buffer.Add(20);

        Assert.Equal([10, 20], buffer.Snapshot());
    }

    [Fact]
    public void RetainsNewestItemsAfterWrapping()
    {
        var buffer = new CircularBuffer<int>(3);

        buffer.Add(10);
        buffer.Add(20);
        buffer.Add(30);
        buffer.Add(40);

        Assert.Equal([20, 30, 40], buffer.Snapshot());
    }

    [Fact]
    public void ClearResetsCountAndContents()
    {
        var buffer = new CircularBuffer<int>(2);
        buffer.Add(1);

        buffer.Clear();

        Assert.Empty(buffer.Snapshot());
        Assert.Equal(0, buffer.Count);
    }
}
