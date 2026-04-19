using System.Diagnostics;
using SysAnalyzer.Capture.Providers;

namespace SysAnalyzer.Capture;

public sealed class PollLoop
{
    private readonly CaptureSession _session;
    private readonly int _intervalMs;
    private readonly SelfOverheadTracker _overheadTracker;

    public SelfOverheadTracker OverheadTracker => _overheadTracker;

    public PollLoop(CaptureSession session, int intervalMs, SelfOverheadTracker? overheadTracker = null)
    {
        _session = session;
        _intervalMs = intervalMs;
        _overheadTracker = overheadTracker ?? new SelfOverheadTracker();
    }

    public async Task RunAsync(CancellationToken ct)
    {
        _overheadTracker.Start();

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_intervalMs));

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var qpcTimestamp = Stopwatch.GetTimestamp();

                var providers = _session.PolledProviders;
                MetricBatch mergedBatch = MetricBatch.Create();
                bool anyData = false;

                foreach (var provider in providers)
                {
                    try
                    {
                        var batch = provider.Poll(qpcTimestamp);
                        if (!batch.IsEmpty)
                        {
                            MergeBatch(ref mergedBatch, batch);
                            anyData = true;
                        }
                    }
                    catch { /* individual provider failure doesn't stop the loop */ }
                }

                if (anyData)
                {
                    var snapshot = CreateSnapshot(qpcTimestamp, mergedBatch);
                    _session.AddSnapshot(snapshot);
                }

                _overheadTracker.Sample();

                try
                {
                    if (!await timer.WaitForNextTickAsync(ct))
                        break;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    private static SensorSnapshot CreateSnapshot(long qpcTimestamp, MetricBatch batch)
    {
        var ts = new QpcTimestamp(qpcTimestamp - QpcTimestamp.CaptureEpoch);

        return new SensorSnapshot(
            Timestamp: ts,
            TotalCpuPercent: batch.TotalCpuPercent,
            PerCoreCpuPercent: Array.Empty<double>(),
            ContextSwitchesPerSec: batch.ContextSwitchesPerSec,
            DpcTimePercent: batch.DpcTimePercent,
            InterruptsPerSec: batch.InterruptsPerSec,
            MemoryUtilizationPercent: batch.MemoryUtilizationPercent,
            AvailableMemoryMb: batch.AvailableMemoryMb,
            PageFaultsPerSec: batch.PageFaultsPerSec,
            HardFaultsPerSec: batch.HardFaultsPerSec,
            CommittedBytes: batch.CommittedBytes,
            CommittedBytesInUsePercent: batch.CommittedBytesInUsePercent,
            GpuUtilizationPercent: double.IsNaN(batch.GpuUtilizationPercent) ? null : batch.GpuUtilizationPercent,
            GpuMemoryUtilizationPercent: double.IsNaN(batch.GpuMemoryUtilizationPercent) ? null : batch.GpuMemoryUtilizationPercent,
            GpuMemoryUsedMb: double.IsNaN(batch.GpuMemoryUsedMb) ? null : batch.GpuMemoryUsedMb,
            DiskActiveTimePercent: batch.DiskActiveTimePercent,
            DiskQueueLength: batch.DiskQueueLength,
            DiskBytesPerSec: batch.DiskBytesPerSec,
            DiskReadLatencyMs: batch.DiskReadLatencyMs,
            DiskWriteLatencyMs: batch.DiskWriteLatencyMs,
            NetworkBytesPerSec: batch.NetworkBytesPerSec,
            NetworkUtilizationPercent: batch.NetworkUtilizationPercent,
            TcpRetransmitsPerSec: batch.TcpRetransmitsPerSec,
            CpuTempC: double.IsNaN(batch.CpuTempC) ? null : batch.CpuTempC,
            CpuClockMhz: double.IsNaN(batch.CpuClockMhz) ? null : batch.CpuClockMhz,
            CpuPowerW: double.IsNaN(batch.CpuPowerW) ? null : batch.CpuPowerW,
            GpuTempC: double.IsNaN(batch.GpuTempC) ? null : batch.GpuTempC,
            GpuClockMhz: double.IsNaN(batch.GpuClockMhz) ? null : batch.GpuClockMhz,
            GpuPowerW: double.IsNaN(batch.GpuPowerW) ? null : batch.GpuPowerW,
            GpuFanRpm: double.IsNaN(batch.GpuFanRpm) ? null : batch.GpuFanRpm
        );
    }

    private static void MergeBatch(ref MetricBatch target, MetricBatch source)
    {
        // Overwrite non-default values
        if (source.TotalCpuPercent != 0) target.TotalCpuPercent = source.TotalCpuPercent;
        if (source.ContextSwitchesPerSec != 0) target.ContextSwitchesPerSec = source.ContextSwitchesPerSec;
        if (source.DpcTimePercent != 0) target.DpcTimePercent = source.DpcTimePercent;
        if (source.InterruptsPerSec != 0) target.InterruptsPerSec = source.InterruptsPerSec;
        if (source.MemoryUtilizationPercent != 0) target.MemoryUtilizationPercent = source.MemoryUtilizationPercent;
        if (source.AvailableMemoryMb != 0) target.AvailableMemoryMb = source.AvailableMemoryMb;
        if (source.PageFaultsPerSec != 0) target.PageFaultsPerSec = source.PageFaultsPerSec;
        if (source.HardFaultsPerSec != 0) target.HardFaultsPerSec = source.HardFaultsPerSec;
        if (source.CommittedBytes != 0) target.CommittedBytes = source.CommittedBytes;
        if (source.CommittedBytesInUsePercent != 0) target.CommittedBytesInUsePercent = source.CommittedBytesInUsePercent;
        if (!double.IsNaN(source.GpuUtilizationPercent)) target.GpuUtilizationPercent = source.GpuUtilizationPercent;
        if (!double.IsNaN(source.GpuMemoryUtilizationPercent)) target.GpuMemoryUtilizationPercent = source.GpuMemoryUtilizationPercent;
        if (!double.IsNaN(source.GpuMemoryUsedMb)) target.GpuMemoryUsedMb = source.GpuMemoryUsedMb;
        if (source.DiskActiveTimePercent != 0) target.DiskActiveTimePercent = source.DiskActiveTimePercent;
        if (source.DiskQueueLength != 0) target.DiskQueueLength = source.DiskQueueLength;
        if (source.DiskBytesPerSec != 0) target.DiskBytesPerSec = source.DiskBytesPerSec;
        if (source.DiskReadLatencyMs != 0) target.DiskReadLatencyMs = source.DiskReadLatencyMs;
        if (source.DiskWriteLatencyMs != 0) target.DiskWriteLatencyMs = source.DiskWriteLatencyMs;
        if (source.NetworkBytesPerSec != 0) target.NetworkBytesPerSec = source.NetworkBytesPerSec;
        if (source.NetworkUtilizationPercent != 0) target.NetworkUtilizationPercent = source.NetworkUtilizationPercent;
        if (source.TcpRetransmitsPerSec != 0) target.TcpRetransmitsPerSec = source.TcpRetransmitsPerSec;
        if (!double.IsNaN(source.CpuTempC)) target.CpuTempC = source.CpuTempC;
        if (!double.IsNaN(source.CpuClockMhz)) target.CpuClockMhz = source.CpuClockMhz;
        if (!double.IsNaN(source.CpuPowerW)) target.CpuPowerW = source.CpuPowerW;
        if (!double.IsNaN(source.GpuTempC)) target.GpuTempC = source.GpuTempC;
        if (!double.IsNaN(source.GpuClockMhz)) target.GpuClockMhz = source.GpuClockMhz;
        if (!double.IsNaN(source.GpuPowerW)) target.GpuPowerW = source.GpuPowerW;
        if (!double.IsNaN(source.GpuFanRpm)) target.GpuFanRpm = source.GpuFanRpm;
    }
}
