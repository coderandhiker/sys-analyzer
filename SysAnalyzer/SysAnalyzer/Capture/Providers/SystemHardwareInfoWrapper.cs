using Hardware.Info;

namespace SysAnalyzer.Capture.Providers;

public sealed class SystemHardwareInfoWrapper : IHardwareInfoWrapper
{
    private readonly HardwareInfo _hw = new();

    public void RefreshCPUList() => _hw.RefreshCPUList();
    public void RefreshMemoryList() => _hw.RefreshMemoryList();
    public void RefreshVideoControllerList() => _hw.RefreshVideoControllerList();
    public void RefreshBIOSList() => _hw.RefreshBIOSList();
    public void RefreshMotherboardList() => _hw.RefreshMotherboardList();
    public void RefreshDriveList() => _hw.RefreshDriveList();
    public void RefreshNetworkAdapterList() => _hw.RefreshNetworkAdapterList();

    public IReadOnlyList<HwCpuInfo> Cpus =>
        _hw.CpuList.Select(c => new HwCpuInfo(
            c.Name.Trim(),
            (int)c.NumberOfCores,
            (int)c.NumberOfLogicalProcessors,
            (int)c.CurrentClockSpeed,
            (int)c.MaxClockSpeed,
            (long)(c.L3CacheSize + c.L2CacheSize) * 1024
        )).ToList();

    public IReadOnlyList<HwMemoryInfo> Memory =>
        _hw.MemoryList.Select(m => new HwMemoryInfo(
            m.BankLabel,
            (long)m.Capacity,
            (int)m.Speed,
            m.MemoryType.ToString()
        )).ToList();

    public IReadOnlyList<HwGpuInfo> Gpus =>
        _hw.VideoControllerList.Select(v => new HwGpuInfo(
            v.Name.Trim(),
            (int)(v.AdapterRAM / (1024 * 1024)),
            v.DriverVersion
        )).ToList();

    public IReadOnlyList<HwDiskInfo> Disks =>
        _hw.DriveList.Select(d => new HwDiskInfo(
            d.Model.Trim(),
            d.MediaType,
            (long)d.Size
        )).ToList();

    public IReadOnlyList<HwNetworkInfo> NetworkAdapters =>
        _hw.NetworkAdapterList.Select(n => new HwNetworkInfo(
            n.Name.Trim(),
            (long)n.Speed
        )).ToList();

    public string MotherboardModel =>
        _hw.MotherboardList.FirstOrDefault()?.Product?.Trim() ?? "Unknown";

    public string BiosVersion =>
        _hw.BiosList.FirstOrDefault()?.Version?.Trim() ?? "Unknown";

    public string BiosDate =>
        _hw.BiosList.FirstOrDefault()?.ReleaseDate ?? "Unknown";
}
