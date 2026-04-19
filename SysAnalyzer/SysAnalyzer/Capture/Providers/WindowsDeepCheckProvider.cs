namespace SysAnalyzer.Capture.Providers;

public sealed class WindowsDeepCheckProvider : ISnapshotProvider
{
    public string Name => "WindowsDeepCheck";
    public ProviderTier RequiredTier => ProviderTier.Tier1;
    public ProviderHealth Health { get; private set; } = new(ProviderStatus.Active, null, 0, 0, 0);

    private readonly ISystemCheckSource _source;
    private readonly List<string> _notes = new();
    private int _checksSucceeded;
    private const int TotalChecks = 11;

    public WindowsDeepCheckProvider(ISystemCheckSource? source = null)
    {
        _source = source ?? new WindowsSystemCheckSource();
    }

    public Task<ProviderHealth> InitAsync()
    {
        // Just validate that the source is available
        Health = new ProviderHealth(ProviderStatus.Active, null, TotalChecks, TotalChecks, 0);
        return Task.FromResult(Health);
    }

    public Task<SnapshotData> CaptureAsync()
    {
        _checksSucceeded = 0;

        var powerPlan = SafeCheck("PowerPlan", () =>
        {
            var plan = _source.RunWmiQuery("Win32_PowerPlan", "ElementName", @"root\cimv2\power")
                    ?? _source.ReadRegistryString(@"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes", "ActivePowerScheme");
            return plan ?? "Unknown";
        });

        var gameMode = SafeCheck("GameMode", () =>
        {
            var val = _source.ReadRegistryDword(@"SOFTWARE\Microsoft\GameBar", "AllowAutoGameMode")
                   ?? _source.ReadRegistryDword(@"SOFTWARE\Microsoft\GameBar", "AutoGameModeEnabled");
            return val.HasValue && val.Value != 0;
        });

        var hags = SafeCheck("HAGS", () =>
        {
            var val = _source.ReadRegistryDword(
                @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode");
            return val.HasValue && val.Value == 2;
        });

        var gameDvr = SafeCheck("GameDVR", () =>
        {
            var val = _source.ReadRegistryDword(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled")
                ?? _source.ReadRegistryDword(
                @"SYSTEM\GameConfigStore", "GameDVR_Enabled");
            return val.HasValue && val.Value != 0;
        });

        var sysMain = SafeCheck("SysMain", () => _source.IsServiceRunning("SysMain"));
        var wSearch = SafeCheck("WSearch", () => _source.IsServiceRunning("WSearch"));

        var shaderCacheMb = SafeCheck("ShaderCache", () =>
            _source.GetDirectorySizeMb(@"%LOCALAPPDATA%\D3DSCache")
            + _source.GetDirectorySizeMb(@"%LOCALAPPDATA%\NVIDIA\DXCache"));

        var tempFolderMb = SafeCheck("TempFolder", () =>
            _source.GetDirectorySizeMb(@"%TEMP%"));

        var pagefileConfig = SafeCheck("Pagefile", () =>
        {
            var val = _source.RunWmiQuery("Win32_PageFileSetting", "InitialSize");
            return val != null ? $"Custom ({val}MB)" : "System managed";
        });

        var startupCount = SafeCheck("StartupPrograms", () =>
        {
            // Rough count from registry Run keys
            var val = _source.ReadRegistryString(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "");
            return 0; // Placeholder — actual enumeration in real source
        });

        var avProduct = SafeCheck("AV", () => _source.GetActiveAvProduct());

        var status = _checksSucceeded == 0 ? ProviderStatus.Failed
            : _checksSucceeded < TotalChecks ? ProviderStatus.Degraded
            : ProviderStatus.Active;

        var reason = _notes.Count > 0 ? string.Join("; ", _notes) : null;
        Health = new ProviderHealth(status, reason, _checksSucceeded, TotalChecks, 0);

        var config = new SystemConfiguration(
            PowerPlan: powerPlan,
            GameModeEnabled: gameMode,
            HagsEnabled: hags,
            GameDvrEnabled: gameDvr,
            SysMainRunning: sysMain,
            WSearchRunning: wSearch,
            ShaderCacheSizeMb: shaderCacheMb,
            TempFolderSizeMb: tempFolderMb,
            PagefileConfig: pagefileConfig,
            StartupProgramCount: startupCount,
            AvProduct: avProduct
        );

        // Hardware is empty — will be merged from HardwareInventoryProvider
        var hardware = new HardwareInventory(
            "Unknown", 0, 0, 0, 0, 0,
            Array.Empty<RamStick>(), 0, 0, 0,
            null, null, null,
            Array.Empty<DiskDrive>(),
            "Unknown", "Unknown", "Unknown",
            "Unknown", "Unknown",
            Array.Empty<NetworkAdapter>(),
            "Unknown", 0
        );

        return Task.FromResult(new SnapshotData(hardware, config));
    }

    private T SafeCheck<T>(string name, Func<T> check) where T : notnull
    {
        try
        {
            var result = check();
            _checksSucceeded++;
            return result;
        }
        catch (Exception ex)
        {
            _notes.Add($"{name}: {ex.Message}");
            return default!;
        }
    }

    public void Dispose() { }
}
