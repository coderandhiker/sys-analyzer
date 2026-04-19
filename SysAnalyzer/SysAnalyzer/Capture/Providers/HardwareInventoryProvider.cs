using System.Runtime.InteropServices;

namespace SysAnalyzer.Capture.Providers;

public sealed class HardwareInventoryProvider : ISnapshotProvider
{
    public string Name => "HardwareInventory";
    public ProviderTier RequiredTier => ProviderTier.Tier1;
    public ProviderHealth Health { get; private set; } = new(ProviderStatus.Active, null, 0, 0, 0);

    private readonly IHardwareInfoWrapper _hw;
    private readonly TimeSpan _timeout;
    private readonly List<string> _notes = new();

    public HardwareInventoryProvider(IHardwareInfoWrapper? hw = null, TimeSpan? timeout = null)
    {
        _hw = hw ?? new SystemHardwareInfoWrapper();
        _timeout = timeout ?? TimeSpan.FromSeconds(30);
    }

    public Task<ProviderHealth> InitAsync()
    {
        int available = 0;
        int expected = 7; // 7 refresh calls

        var refreshActions = new (string name, Action action)[]
        {
            ("CPU", _hw.RefreshCPUList),
            ("Memory", _hw.RefreshMemoryList),
            ("GPU", _hw.RefreshVideoControllerList),
            ("BIOS", _hw.RefreshBIOSList),
            ("Motherboard", _hw.RefreshMotherboardList),
            ("Disk", _hw.RefreshDriveList),
            ("Network", _hw.RefreshNetworkAdapterList),
        };

        foreach (var (name, action) in refreshActions)
        {
            try
            {
                var task = Task.Run(action);
                if (task.Wait(_timeout))
                {
                    available++;
                }
                else
                {
                    _notes.Add($"{name}: WMI timeout after {_timeout.TotalSeconds}s");
                }
            }
            catch (Exception ex)
            {
                _notes.Add($"{name}: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        var status = available == 0 ? ProviderStatus.Failed
            : available < expected ? ProviderStatus.Degraded
            : ProviderStatus.Active;

        var reason = _notes.Count > 0 ? string.Join("; ", _notes) : null;
        Health = new ProviderHealth(status, reason, available, expected, 0);
        return Task.FromResult(Health);
    }

    public Task<SnapshotData> CaptureAsync()
    {
        var cpu = _hw.Cpus.FirstOrDefault();
        var gpu = _hw.Gpus.FirstOrDefault();
        var ramSticks = _hw.Memory.Select(m => new RamStick(
            m.BankLabel, m.CapacityBytes, m.SpeedMhz, m.MemoryType)).ToList();
        var disks = _hw.Disks.Select(d => new DiskDrive(
            d.Model, d.DriveType, d.SizeBytes)).ToList();
        var adapters = _hw.NetworkAdapters
            .Where(n => n.SpeedBps > 0)
            .Select(n => new NetworkAdapter(n.Name, n.SpeedBps)).ToList();

        int totalRamGb = (int)(ramSticks.Sum(r => r.CapacityBytes) / (1024L * 1024 * 1024));

        // Get display info from system
        string displayRes = "Unknown";
        int refreshRate = 0;
        try
        {
            displayRes = $"{GetSystemMetrics(0)}x{GetSystemMetrics(1)}";
            // Refresh rate from WMI or default
            refreshRate = 60;
        }
        catch { }

        var hardware = new HardwareInventory(
            CpuModel: cpu?.Name ?? "Unknown",
            CpuCores: cpu?.Cores ?? 0,
            CpuThreads: cpu?.Threads ?? 0,
            CpuBaseClock: cpu?.BaseClockMhz ?? 0,
            CpuMaxBoostClock: cpu?.MaxBoostClockMhz ?? 0,
            CpuCacheBytes: cpu?.CacheBytes ?? 0,
            RamSticks: ramSticks,
            TotalRamGb: totalRamGb,
            TotalMemorySlots: ramSticks.Count,
            AvailableMemorySlots: 0,
            GpuModel: gpu?.Name,
            GpuVramMb: gpu?.VramMb,
            GpuDriverVersion: gpu?.DriverVersion,
            Disks: disks,
            MotherboardModel: _hw.MotherboardModel,
            BiosVersion: _hw.BiosVersion,
            BiosDate: _hw.BiosDate,
            OsBuild: Environment.OSVersion.Version.Build.ToString(),
            OsVersion: RuntimeInformation.OSDescription,
            NetworkAdapters: adapters,
            DisplayResolution: displayRes,
            DisplayRefreshRate: refreshRate
        );

        // SystemConfiguration will be filled by WindowsDeepCheckProvider
        var config = new SystemConfiguration(
            PowerPlan: "Unknown",
            GameModeEnabled: false,
            HagsEnabled: false,
            GameDvrEnabled: false,
            SysMainRunning: false,
            WSearchRunning: false,
            ShaderCacheSizeMb: 0,
            TempFolderSizeMb: 0,
            PagefileConfig: "Unknown",
            StartupProgramCount: 0,
            AvProduct: "Unknown"
        );

        return Task.FromResult(new SnapshotData(hardware, config));
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    public void Dispose() { }
}
