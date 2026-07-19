using System.Net.NetworkInformation;
using WindowsTaskbarMonitor.Core;

namespace WindowsTaskbarMonitor.App.Metrics;

internal sealed class NetworkRateReader
{
    private readonly CounterRate _downloadRate = new();
    private readonly CounterRate _uploadRate = new();

    public NetworkSample Read(DateTimeOffset timestamp)
    {
        ulong received = 0;
        ulong sent = 0;

        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.OperationalStatus != OperationalStatus.Up ||
                adapter.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
            {
                continue;
            }

            try
            {
                var statistics = adapter.GetIPStatistics();
                received += (ulong)Math.Max(0, statistics.BytesReceived);
                sent += (ulong)Math.Max(0, statistics.BytesSent);
            }
            catch (NetworkInformationException)
            {
                // A network adapter can disappear between enumeration and sampling.
            }
        }

        return new NetworkSample(
            _downloadRate.Next(received, timestamp),
            _uploadRate.Next(sent, timestamp));
    }
}

internal readonly record struct NetworkSample(double DownloadBytesPerSecond, double UploadBytesPerSecond);
