namespace SysAnalyzer.Capture.Providers;

public interface IHardwareInfoWrapper
{
    void RefreshCPUList();
    void RefreshMemoryList();
    void RefreshVideoControllerList();
    void RefreshBIOSList();
    void RefreshMotherboardList();
    void RefreshDriveList();
    void RefreshNetworkAdapterList();

    IReadOnlyList<HwCpuInfo> Cpus { get; }
    IReadOnlyList<HwMemoryInfo> Memory { get; }
    IReadOnlyList<HwGpuInfo> Gpus { get; }
    IReadOnlyList<HwDiskInfo> Disks { get; }
    IReadOnlyList<HwNetworkInfo> NetworkAdapters { get; }
    string MotherboardModel { get; }
    string BiosVersion { get; }
    string BiosDate { get; }
}

public record HwCpuInfo(string Name, int Cores, int Threads, int BaseClockMhz, int MaxBoostClockMhz, long CacheBytes);
public record HwMemoryInfo(string BankLabel, long CapacityBytes, int SpeedMhz, string MemoryType);
public record HwGpuInfo(string Name, int VramMb, string DriverVersion);
public record HwDiskInfo(string Model, string DriveType, long SizeBytes);
public record HwNetworkInfo(string Name, long SpeedBps);
