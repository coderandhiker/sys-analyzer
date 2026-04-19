namespace SysAnalyzer.Analysis.Models;

/// <summary>
/// Full culprit attribution result from ETW correlation.
/// </summary>
public record CulpritAttributionResult(
    IReadOnlyList<ProcessCulprit> TopContextSwitchProcesses,
    IReadOnlyList<DriverCulprit> TopDpcDrivers,
    IReadOnlyList<ProcessCulprit> TopDiskIoProcesses,
    IReadOnlyList<ProcessLifetimeEntry> ProcessLifetimeEvents,
    bool HasAttribution,
    bool HasDpcAttribution,
    float InterferenceCorrelation
);

public record ProcessCulprit(
    string ProcessName,
    int ProcessId,
    int ContextSwitchCount,
    float PercentOfTotal,
    float CorrelationWithStutter,
    string? Description,
    string? Remediation
);

public record DriverCulprit(
    string DriverModule,
    double TotalDpcTimeMs,
    float PercentOfDpcTime,
    string? Description
);

public record ProcessLifetimeEntry(
    string ProcessName,
    int ProcessId,
    bool IsStart,
    double TimestampSeconds,
    bool CorrelatesWithStutterCluster
);
