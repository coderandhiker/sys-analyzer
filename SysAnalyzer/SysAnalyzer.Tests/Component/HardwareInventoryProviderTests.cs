using SysAnalyzer.Capture;
using SysAnalyzer.Capture.Providers;

namespace SysAnalyzer.Tests.Component;

public class HardwareInventoryProviderTests
{
    private class FakeHardwareInfoWrapper : IHardwareInfoWrapper
    {
        public bool ShouldThrow { get; set; }
        public bool ShouldTimeout { get; set; }

        public IReadOnlyList<HwCpuInfo> Cpus => new[]
        {
            new HwCpuInfo("AMD Ryzen 7 5800X", 8, 16, 3800, 4700, 33554432L)
        };

        public IReadOnlyList<HwMemoryInfo> Memory => new[]
        {
            new HwMemoryInfo("DIMM_A1", 17179869184L, 3600, "DDR4"),
            new HwMemoryInfo("DIMM_B1", 17179869184L, 3600, "DDR4")
        };

        public IReadOnlyList<HwGpuInfo> Gpus => new[]
        {
            new HwGpuInfo("NVIDIA RTX 3080", 10240, "560.94")
        };

        public IReadOnlyList<HwDiskInfo> Disks => new[]
        {
            new HwDiskInfo("WD_BLACK SN850X", "NVMe", 1000204886016L)
        };

        public IReadOnlyList<HwNetworkInfo> NetworkAdapters => new[]
        {
            new HwNetworkInfo("Intel I225-V", 2500000000L)
        };

        public string MotherboardModel => "ASUS B550-F";
        public string BiosVersion => "2803";
        public string BiosDate => "2024-01-15";

        public void RefreshCPUList() { if (ShouldThrow) throw new Exception("CPU fail"); }
        public void RefreshMemoryList() { if (ShouldThrow) throw new Exception("Memory fail"); }
        public void RefreshVideoControllerList() { if (ShouldThrow) throw new Exception("GPU fail"); }
        public void RefreshBIOSList() { if (ShouldThrow) throw new Exception("BIOS fail"); }
        public void RefreshMotherboardList() { if (ShouldThrow) throw new Exception("Mobo fail"); }
        public void RefreshDriveList() { if (ShouldThrow) throw new Exception("Disk fail"); }
        public void RefreshNetworkAdapterList() { if (ShouldThrow) throw new Exception("Network fail"); }
    }

    [Fact]
    public async Task Init_AllRefreshesSucceed_Active()
    {
        var hw = new FakeHardwareInfoWrapper();
        var provider = new HardwareInventoryProvider(hw);
        var health = await provider.InitAsync();

        Assert.Equal(ProviderStatus.Active, health.Status);
        Assert.Equal(7, health.MetricsAvailable);
    }

    [Fact]
    public async Task Init_AllRefreshesFail_Failed()
    {
        var hw = new FakeHardwareInfoWrapper { ShouldThrow = true };
        var provider = new HardwareInventoryProvider(hw);
        var health = await provider.InitAsync();

        Assert.Equal(ProviderStatus.Failed, health.Status);
        Assert.Equal(0, health.MetricsAvailable);
    }

    [Fact]
    public async Task CaptureAsync_ReturnsHardwareInventory()
    {
        var hw = new FakeHardwareInfoWrapper();
        var provider = new HardwareInventoryProvider(hw);
        await provider.InitAsync();

        var data = await provider.CaptureAsync();

        Assert.Equal("AMD Ryzen 7 5800X", data.Hardware.CpuModel);
        Assert.Equal(8, data.Hardware.CpuCores);
        Assert.Equal(16, data.Hardware.CpuThreads);
        Assert.Equal(2, data.Hardware.RamSticks.Count);
        Assert.Equal(32, data.Hardware.TotalRamGb);
        Assert.Equal("NVIDIA RTX 3080", data.Hardware.GpuModel);
        Assert.Equal(10240, data.Hardware.GpuVramMb);
        Assert.Single(data.Hardware.Disks);
        Assert.Single(data.Hardware.NetworkAdapters);
    }

    [Fact]
    public void Name_IsHardwareInventory()
    {
        var provider = new HardwareInventoryProvider(new FakeHardwareInfoWrapper());
        Assert.Equal("HardwareInventory", provider.Name);
    }

    [Fact]
    public void RequiredTier_IsTier1()
    {
        var provider = new HardwareInventoryProvider(new FakeHardwareInfoWrapper());
        Assert.Equal(ProviderTier.Tier1, provider.RequiredTier);
    }
}
