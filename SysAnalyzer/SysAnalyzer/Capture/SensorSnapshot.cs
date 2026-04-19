namespace SysAnalyzer.Capture;

/// <summary>
/// One point-in-time snapshot of all polled metrics.
/// Immutable record — GPU and Tier 2 fields are nullable.
/// </summary>
public record SensorSnapshot(
    QpcTimestamp Timestamp,

    // CPU metrics
    double TotalCpuPercent,
    double[] PerCoreCpuPercent,
    double ContextSwitchesPerSec,
    double DpcTimePercent,
    double InterruptsPerSec,

    // Memory metrics
    double MemoryUtilizationPercent,
    double AvailableMemoryMb,
    double PageFaultsPerSec,
    double HardFaultsPerSec,
    double CommittedBytes,
    double CommittedBytesInUsePercent,

    // GPU metrics (nullable — may be absent)
    double? GpuUtilizationPercent,
    double? GpuMemoryUtilizationPercent,
    double? GpuMemoryUsedMb,

    // Disk metrics
    double DiskActiveTimePercent,
    double DiskQueueLength,
    double DiskBytesPerSec,
    double DiskReadLatencyMs,
    double DiskWriteLatencyMs,

    // Network metrics
    double NetworkBytesPerSec,
    double NetworkUtilizationPercent,
    double TcpRetransmitsPerSec,

    // Tier 2 (nullable)
    double? CpuTempC,
    double? CpuClockMhz,
    double? CpuPowerW,
    double? GpuTempC,
    double? GpuClockMhz,
    double? GpuPowerW,
    double? GpuFanRpm
);
