# Architecture and metric semantics

## Process model

The application is a single, unpackaged, self-contained WinUI 3 process. A named mutex prevents duplicate instances. The process creates a Win32 message-only window for its notification icon and keeps the dashboard window hidden until the user opens it.

Sampling occurs on one background task at a one-second interval. Results are marshalled to the WinUI dispatcher for presentation and tray-icon rendering. A collector exception is contained to its sampling iteration so a transient adapter, driver, or performance-counter failure cannot terminate the process.

## Metric sources

### CPU

`GetSystemTimes` returns cumulative idle, kernel, and user time. Utilization is calculated from the change between samples. Kernel time includes idle time, so the busy fraction is `(kernel delta + user delta - idle delta) / total delta`.

CPU package temperature comes from a supported LibreHardwareMonitor sensor. Preferred names are CPU Package, Core Average, and CPU (Tctl/Tdie); otherwise the highest valid CPU temperature is used.

### GPU

LibreHardwareMonitor is preferred for GPU utilization and temperature. If it has no load sensor, the collector reads Windows `GPU Engine(*)\\Utilization Percentage` counters. Per-process values are grouped by physical engine, summed within each engine, and the busiest engine becomes the displayed utilization. Values are bounded to 0–100 percent.

### Memory

`GlobalMemoryStatusEx` provides total and available physical memory. Used memory is `total - available`, and the percentage is derived from those byte values.

### Disk

PDH English counters read aggregate `PhysicalDisk(_Total)` read and write bytes per second. The display is aggregate throughput rather than capacity or per-volume activity.

### Network

The collector sums byte counters for active adapters other than loopback and tunnel interfaces. Consecutive totals become per-second download and upload rates. If an adapter disappears and the aggregate counter falls, the rate calculator treats it as a reset instead of reporting a negative value.

## Failure behavior

- Missing or inaccessible temperature sensor: display Unavailable.
- Missing GPU Engine counter: use a hardware load sensor or display Unavailable.
- PDH failure: report zero disk throughput for that sample.
- Adapter enumeration race: skip the affected adapter.
- Counter reset or non-increasing timestamp: report zero for that interval.
- Explorer restart: respond to `TaskbarCreated` and register the notification icon again.

## Privacy

No sampled value leaves the process. The only persistent data is a small JSON settings file under the current user's local application-data directory containing the selected tray metric.
