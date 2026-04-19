namespace SysAnalyzer.Capture;

/// <summary>
/// Lightweight struct returned by IPolledProvider.Poll().
/// Pre-allocated, no heap allocation on the hot path.
/// Each polled provider fills only its relevant fields.
/// </summary>
public struct MetricBatch
{
    // CPU metrics
    public double TotalCpuPercent;
    public double ContextSwitchesPerSec;
    public double DpcTimePercent;
    public double InterruptsPerSec;

    // Memory metrics
    public double MemoryUtilizationPercent;
    public double AvailableMemoryMb;
    public double PageFaultsPerSec;
    public double HardFaultsPerSec;
    public double CommittedBytes;
    public double CommittedBytesInUsePercent;

    // GPU metrics (NaN = not available)
    public double GpuUtilizationPercent;
    public double GpuMemoryUtilizationPercent;
    public double GpuMemoryUsedMb;

    // Disk metrics
    public double DiskActiveTimePercent;
    public double DiskQueueLength;
    public double DiskBytesPerSec;
    public double DiskReadLatencyMs;
    public double DiskWriteLatencyMs;

    // Network metrics
    public double NetworkBytesPerSec;
    public double NetworkUtilizationPercent;
    public double TcpRetransmitsPerSec;

    // Tier 2 (NaN = not available)
    public double CpuTempC;
    public double CpuClockMhz;
    public double CpuPowerW;
    public double GpuTempC;
    public double GpuClockMhz;
    public double GpuPowerW;
    public double GpuFanRpm;

    /// <summary>Whether this batch has any valid data.</summary>
    public bool IsEmpty;

    /// <summary>A batch with no valid data. Returned on poll failure.</summary>
    public static MetricBatch Empty => new() { IsEmpty = true };

    /// <summary>Create a new batch with all optional fields set to NaN.</summary>
    public static MetricBatch Create() => new()
    {
        GpuUtilizationPercent = double.NaN,
        GpuMemoryUtilizationPercent = double.NaN,
        GpuMemoryUsedMb = double.NaN,
        CpuTempC = double.NaN,
        CpuClockMhz = double.NaN,
        CpuPowerW = double.NaN,
        GpuTempC = double.NaN,
        GpuClockMhz = double.NaN,
        GpuPowerW = double.NaN,
        GpuFanRpm = double.NaN,
        IsEmpty = false
    };
}
