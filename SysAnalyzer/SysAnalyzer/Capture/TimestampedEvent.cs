namespace SysAnalyzer.Capture;

/// <summary>
/// Base record for all timestamped events (ETW, PresentMon, etc.).
/// </summary>
public abstract record TimestampedEvent(QpcTimestamp Timestamp);

/// <summary>
/// One PresentMon frame-time row.
/// </summary>
public record FrameTimeSample(
    QpcTimestamp Timestamp,
    string ApplicationName,
    double FrameTimeMs,
    double CpuBusyMs,
    double GpuBusyMs,
    bool Dropped,
    string PresentMode,
    bool AllowsTearing
) : TimestampedEvent(Timestamp);

/// <summary>
/// Base record for ETW-sourced events.
/// </summary>
public abstract record EtwEvent(QpcTimestamp Timestamp) : TimestampedEvent(Timestamp);

/// <summary>Context switch event from ETW kernel process provider.</summary>
public record ContextSwitchEvent(
    QpcTimestamp Timestamp,
    int OldProcessId,
    int NewProcessId,
    string NewProcessName
) : EtwEvent(Timestamp);

/// <summary>Disk I/O completion event from ETW kernel disk provider.</summary>
public record DiskIoEvent(
    QpcTimestamp Timestamp,
    int ProcessId,
    string ProcessName,
    long BytesTransferred
) : EtwEvent(Timestamp);

/// <summary>DPC (deferred procedure call) event for driver latency analysis.</summary>
public record DpcEvent(
    QpcTimestamp Timestamp,
    string DriverModule,
    double DurationUs
) : EtwEvent(Timestamp);

/// <summary>Process start/stop event for lifetime tracking.</summary>
public record ProcessLifetimeEvent(
    QpcTimestamp Timestamp,
    int ProcessId,
    string ProcessName,
    bool IsStart
) : EtwEvent(Timestamp);
