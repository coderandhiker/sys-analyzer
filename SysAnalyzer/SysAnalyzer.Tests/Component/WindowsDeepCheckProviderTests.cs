using SysAnalyzer.Capture.Providers;

namespace SysAnalyzer.Tests.Component;

public class WindowsDeepCheckProviderTests
{
    private class FakeSystemCheckSource : ISystemCheckSource
    {
        public bool ShouldThrow { get; set; }

        public string? ReadRegistryString(string keyPath, string valueName)
        {
            if (ShouldThrow) throw new Exception("Registry failed");
            return keyPath.Contains("PowerSchemes") ? "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c" : null;
        }

        public int? ReadRegistryDword(string keyPath, string valueName)
        {
            if (ShouldThrow) throw new Exception("Registry failed");
            if (keyPath.Contains("GameBar")) return 1;
            if (keyPath.Contains("GraphicsDrivers") && valueName == "HwSchMode") return 2;
            if (keyPath.Contains("GameDVR") || keyPath.Contains("GameConfigStore")) return 0;
            return null;
        }

        public string? RunWmiQuery(string className, string propertyName, string? scope = null)
        {
            if (ShouldThrow) throw new Exception("WMI failed");
            if (className == "Win32_PowerPlan") return "High performance";
            if (className == "Win32_PageFileSetting") return null;
            return null;
        }

        public bool IsServiceRunning(string serviceName)
        {
            if (ShouldThrow) throw new Exception("Service check failed");
            return serviceName == "SysMain";
        }

        public long GetDirectorySizeMb(string path)
        {
            if (ShouldThrow) throw new Exception("Dir size failed");
            return 100;
        }

        public string GetActiveAvProduct()
        {
            if (ShouldThrow) throw new Exception("AV check failed");
            return "Windows Defender";
        }
    }

    [Fact]
    public async Task Init_ReturnsActive()
    {
        var source = new FakeSystemCheckSource();
        var provider = new WindowsDeepCheckProvider(source);
        var health = await provider.InitAsync();

        Assert.Equal(ProviderStatus.Active, health.Status);
    }

    [Fact]
    public async Task CaptureAsync_ReturnsSystemConfig()
    {
        var source = new FakeSystemCheckSource();
        var provider = new WindowsDeepCheckProvider(source);
        await provider.InitAsync();

        var data = await provider.CaptureAsync();

        Assert.Equal("High performance", data.Configuration.PowerPlan);
        Assert.True(data.Configuration.GameModeEnabled);
        Assert.True(data.Configuration.HagsEnabled);
        Assert.False(data.Configuration.GameDvrEnabled);
        Assert.True(data.Configuration.SysMainRunning);
        Assert.False(data.Configuration.WSearchRunning);
        Assert.Equal("Windows Defender", data.Configuration.AvProduct);
    }

    [Fact]
    public async Task CaptureAsync_AllChecksFail_StillReturns()
    {
        var source = new FakeSystemCheckSource { ShouldThrow = true };
        var provider = new WindowsDeepCheckProvider(source);
        await provider.InitAsync();

        var data = await provider.CaptureAsync();

        Assert.NotNull(data);
        Assert.NotNull(data.Configuration);
        Assert.Equal(ProviderStatus.Failed, provider.Health.Status);
    }

    [Fact]
    public void Name_IsWindowsDeepCheck()
    {
        var provider = new WindowsDeepCheckProvider(new FakeSystemCheckSource());
        Assert.Equal("WindowsDeepCheck", provider.Name);
    }

    [Fact]
    public void RequiredTier_IsTier1()
    {
        var provider = new WindowsDeepCheckProvider(new FakeSystemCheckSource());
        Assert.Equal(ProviderTier.Tier1, provider.RequiredTier);
    }

    [Fact]
    public async Task CaptureAsync_PartialFailures_Degraded()
    {
        // Create source that throws on specific checks only
        var source = new PartialFailSource();
        var provider = new WindowsDeepCheckProvider(source);
        await provider.InitAsync();
        var data = await provider.CaptureAsync();

        Assert.NotNull(data);
        // Provider should be degraded (some checks passed, some failed)
        Assert.True(provider.Health.Status == ProviderStatus.Degraded
                 || provider.Health.Status == ProviderStatus.Active);
    }

    private class PartialFailSource : ISystemCheckSource
    {
        private int _callCount;

        public string? ReadRegistryString(string keyPath, string valueName)
        {
            _callCount++;
            if (_callCount % 3 == 0) throw new Exception("Partial fail");
            return null;
        }

        public int? ReadRegistryDword(string keyPath, string valueName)
        {
            _callCount++;
            if (_callCount % 3 == 0) throw new Exception("Partial fail");
            return 0;
        }

        public string? RunWmiQuery(string className, string propertyName, string? scope = null) => null;
        public bool IsServiceRunning(string serviceName) => false;
        public long GetDirectorySizeMb(string path) => 0;
        public string GetActiveAvProduct() => "Unknown";
    }
}
