namespace SysAnalyzer.Capture;

/// <summary>
/// Result from ISnapshotProvider.CaptureAsync(). Contains static hardware + system config data.
/// </summary>
public record SnapshotData(
    HardwareInventory Hardware,
    SystemConfiguration Configuration
);

/// <summary>Individual RAM stick info.</summary>
public record RamStick(
    string BankLabel,
    long CapacityBytes,
    int SpeedMhz,
    string MemoryType
);

/// <summary>Disk drive info.</summary>
public record DiskDrive(
    string Model,
    string DriveType,
    long SizeBytes
);

/// <summary>Network adapter info.</summary>
public record NetworkAdapter(
    string Name,
    long SpeedBps
);

/// <summary>
/// Complete hardware inventory captured once at session start.
/// </summary>
public record HardwareInventory(
    // CPU
    string CpuModel,
    int CpuCores,
    int CpuThreads,
    int CpuBaseClock,
    int CpuMaxBoostClock,
    long CpuCacheBytes,

    // RAM
    IReadOnlyList<RamStick> RamSticks,
    int TotalRamGb,
    int TotalMemorySlots,
    int AvailableMemorySlots,

    // GPU
    string? GpuModel,
    int? GpuVramMb,
    string? GpuDriverVersion,

    // Disk
    IReadOnlyList<DiskDrive> Disks,

    // Motherboard & BIOS
    string MotherboardModel,
    string BiosVersion,
    string BiosDate,

    // OS
    string OsBuild,
    string OsVersion,

    // Network
    IReadOnlyList<NetworkAdapter> NetworkAdapters,

    // Display
    string DisplayResolution,
    int DisplayRefreshRate
);

/// <summary>
/// System configuration state captured once at session start.
/// </summary>
public record SystemConfiguration(
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
