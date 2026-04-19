using System.Text.Json.Serialization;

namespace SysAnalyzer.Analysis.Models;

/// <summary>
/// Top-level analysis summary — the contract that JSON, HTML, and baseline comparison all build against.
/// </summary>
public record AnalysisSummary(
    AnalysisMetadata Metadata,
    MachineFingerprint Fingerprint,
    SensorHealthSummary SensorHealth,
    HardwareInventorySummary HardwareInventory,
    SystemConfigurationSummary SystemConfiguration,
    ScoresSummary Scores,
    FrameTimeSummary? FrameTime,
    CulpritAttribution? CulpritAttribution,
    IReadOnlyList<RecommendationEntry> Recommendations,
    BaselineComparisonSummary? BaselineComparison,
    SelfOverhead SelfOverhead,
    TimeSeriesMetadata TimeSeries
);

public record AnalysisMetadata(
    string Version,
    DateTime Timestamp,
    double DurationSeconds,
    string? Label,
    string Profile,
    string Tier,
    string CaptureId
);

public record SensorHealthSummary(
    string OverallTier,
    IReadOnlyList<ProviderHealthEntry> Providers
);

public record ProviderHealthEntry(
    string Name,
    string Status,
    string? DegradationReason,
    int MetricsAvailable,
    int MetricsExpected,
    int EventsLost
);

public record HardwareInventorySummary(
    string CpuModel,
    int CpuCores,
    int CpuThreads,
    int CpuBaseClockMhz,
    int CpuMaxBoostClockMhz,
    long CpuCacheBytes,
    IReadOnlyList<RamStickSummary> RamSticks,
    int TotalRamGb,
    int TotalMemorySlots,
    int AvailableMemorySlots,
    string? GpuModel,
    int? GpuVramMb,
    string? GpuDriverVersion,
    IReadOnlyList<DiskDriveSummary> Disks,
    string MotherboardModel,
    string BiosVersion,
    string BiosDate,
    string OsBuild,
    string OsVersion,
    IReadOnlyList<NetworkAdapterSummary> NetworkAdapters,
    string DisplayResolution,
    int DisplayRefreshRate
);

public record RamStickSummary(string BankLabel, long CapacityBytes, int SpeedMhz, string MemoryType);
public record DiskDriveSummary(string Model, string DriveType, long SizeBytes);
public record NetworkAdapterSummary(string Name, long SpeedBps);

public record SystemConfigurationSummary(
    string PowerPlan,
    bool GameModeEnabled,
    bool HagsEnabled,
    bool GameDvrEnabled,
    bool SysMainRunning,
    bool WSearchRunning,
    long ShaderCacheSizeMb,
    long TempFolderSizeMb,
    string PagefileConfig,
    int StartupProgramCount,
    string AvProduct
);

public record ScoresSummary(
    CategoryScore Cpu,
    CategoryScore Memory,
    CategoryScore? Gpu,
    CategoryScore Disk,
    CategoryScore Network
);

public record CategoryScore(
    int Score,
    string Classification,
    int AvailableMetrics,
    int TotalMetrics
);

public record FrameTimeSummary(
    double AvgFps,
    double P50FrameTimeMs,
    double P95FrameTimeMs,
    double P99FrameTimeMs,
    double P999FrameTimeMs,
    double DroppedFramePct,
    double CpuBoundPct,
    double GpuBoundPct,
    string PresentMode,
    int StutterCount
);

public record CulpritAttribution(
    IReadOnlyList<ProcessEntry> TopProcesses,
    IReadOnlyList<DpcDriverEntry> TopDpcDrivers,
    IReadOnlyList<DiskProcessEntry> TopDiskProcesses
);

public record ProcessEntry(string Name, double ContextSwitchPct, string? Description);
public record DpcDriverEntry(string Module, double DpcTimePct);
public record DiskProcessEntry(string Name, double DiskIoPct);

public record RecommendationEntry(
    string Id,
    string Title,
    string Body,
    string Severity,
    string Category,
    string Confidence,
    int Priority,
    IReadOnlyList<string> Evidence
);

public record BaselineComparisonSummary(
    string BaselineId,
    bool FingerprintMatch,
    IReadOnlyList<DeltaEntry> Deltas
);

public record DeltaEntry(string Metric, double BaselineValue, double CurrentValue, double Change);

public record SelfOverhead(
    double AvgCpuPercent,
    long PeakWorkingSetBytes,
    int GcCollections,
    double GcPauseTimeMs,
    int EtwEventsLost
);

public record TimeSeriesMetadata(
    int SampleCount,
    double DurationSeconds,
    int DownsampleFactor
);
