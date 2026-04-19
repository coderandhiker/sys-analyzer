using System.Text.Json;
using FluentAssertions;
using SysAnalyzer.Analysis.Models;
using SysAnalyzer.Report;

namespace SysAnalyzer.Tests.Unit;

public class JsonSchemaRoundTripTests
{
    private static AnalysisSummary CreateFullSummary() => new(
        Metadata: new AnalysisMetadata(
            Version: "1.0.0",
            Timestamp: new DateTime(2026, 4, 19, 14, 32, 5, DateTimeKind.Utc),
            DurationSeconds: 120.5,
            Label: "cyberpunk-ultra-4k",
            Profile: "gaming",
            Tier: "Tier2",
            CaptureId: "cap-20260419-143205"
        ),
        Fingerprint: new MachineFingerprint(
            CpuModel: "AMD Ryzen 7 5800X",
            GpuModel: "NVIDIA GeForce RTX 3080",
            TotalRamGb: 32,
            RamConfig: "2x16GB DDR4-3600",
            OsBuild: "22631.3880",
            DisplayConfig: "2560x1440@144Hz",
            StorageConfigHash: "nvme-wd_sn850x",
            GpuDriverMajorVersion: "560",
            MotherboardModel: "ASUS ROG STRIX B550-F"
        ),
        SensorHealth: new SensorHealthSummary(
            OverallTier: "Tier2",
            Providers: new[]
            {
                new ProviderHealthEntry("PerformanceCounters", "Active", null, 15, 15, 0),
                new ProviderHealthEntry("LibreHardwareMonitor", "Active", null, 7, 7, 0)
            }
        ),
        HardwareInventory: new HardwareInventorySummary(
            CpuModel: "AMD Ryzen 7 5800X",
            CpuCores: 8,
            CpuThreads: 16,
            CpuBaseClockMhz: 3800,
            CpuMaxBoostClockMhz: 4700,
            CpuCacheBytes: 33554432,
            RamSticks: new[] { new RamStickSummary("DIMM_A1", 17179869184, 3600, "DDR4") },
            TotalRamGb: 32,
            TotalMemorySlots: 4,
            AvailableMemorySlots: 2,
            GpuModel: "NVIDIA GeForce RTX 3080",
            GpuVramMb: 10240,
            GpuDriverVersion: "560.94",
            Disks: new[] { new DiskDriveSummary("WD_BLACK SN850X 1TB", "NVMe", 1000204886016) },
            MotherboardModel: "ASUS ROG STRIX B550-F",
            BiosVersion: "2803",
            BiosDate: "2024-01-15",
            OsBuild: "22631.3880",
            OsVersion: "Windows 11 23H2",
            NetworkAdapters: new[] { new NetworkAdapterSummary("Intel I225-V", 2500000000) },
            DisplayResolution: "2560x1440",
            DisplayRefreshRate: 144
        ),
        SystemConfiguration: new SystemConfigurationSummary(
            PowerPlan: "High performance",
            GameModeEnabled: true,
            HagsEnabled: true,
            GameDvrEnabled: false,
            SysMainRunning: false,
            WSearchRunning: true,
            ShaderCacheSizeMb: 450,
            TempFolderSizeMb: 1200,
            PagefileConfig: "System managed",
            StartupProgramCount: 12,
            AvProduct: "Windows Defender"
        ),
        Scores: new ScoresSummary(
            Cpu: new CategoryScore(45, "Moderate", 6, 6),
            Memory: new CategoryScore(30, "Healthy", 5, 5),
            Gpu: new CategoryScore(82, "Stressed", 6, 6),
            Disk: new CategoryScore(15, "Healthy", 4, 4),
            Network: new CategoryScore(5, "Healthy", 3, 3)
        ),
        FrameTime: new FrameTimeSummary(72.5, 13.8, 18.2, 28.5, 45.1, 0.3, 15.2, 78.5, "Hardware: Independent Flip", 7),
        CulpritAttribution: new CulpritAttribution(
            TopProcesses: new[] { new ProcessEntry("dwm.exe", 12.5, "Desktop Window Manager") },
            TopDpcDrivers: new[] { new DpcDriverEntry("ndis.sys", 2.1) },
            TopDiskProcesses: new[] { new DiskProcessEntry("SearchIndexer.exe", 35.0) }
        ),
        Recommendations: new[]
        {
            new RecommendationEntry(
                "ft_gpu_bound", "GPU-Bound Rendering", "78.5% of frames were GPU-bound.",
                "warning", "frametime", "high", 8, new[] { "gpu_bound_frame_pct=78.5" }
            )
        },
        BaselineComparison: new BaselineComparisonSummary(
            "cap-20260417-100000", true,
            new[] { new DeltaEntry("cpu.avg_load", 52.3, 45.0, -7.3) }
        ),
        SelfOverhead: new SelfOverhead(0.8, 67108864, 3, 1.2, 0),
        TimeSeries: new TimeSeriesMetadata(120, 120.5, 1)
    );

    private static AnalysisSummary CreateSparseSummary() => new(
        Metadata: new AnalysisMetadata("1.0.0", new DateTime(2026, 4, 19, 14, 32, 5, DateTimeKind.Utc), 30.0, null, "general_interactive", "Tier1", "cap-sparse"),
        Fingerprint: new MachineFingerprint("AMD Ryzen 7 5800X", "NVIDIA GeForce RTX 3080", 32, "2x16GB DDR4-3600", "22631.3880", "2560x1440@144Hz", "nvme", "560", "ASUS B550"),
        SensorHealth: new SensorHealthSummary("Tier1", new[] { new ProviderHealthEntry("PerformanceCounters", "Active", null, 10, 15, 0) }),
        HardwareInventory: new HardwareInventorySummary("AMD Ryzen 7 5800X", 8, 16, 3800, 4700, 33554432, Array.Empty<RamStickSummary>(), 32, 4, 2, null, null, null, Array.Empty<DiskDriveSummary>(), "ASUS B550", "1.0", "2024-01-01", "22631", "Windows 11", Array.Empty<NetworkAdapterSummary>(), "1920x1080", 60),
        SystemConfiguration: new SystemConfigurationSummary("Balanced", false, false, true, true, true, 100, 500, "System managed", 20, "Windows Defender"),
        Scores: new ScoresSummary(new CategoryScore(20, "Healthy", 4, 6), new CategoryScore(10, "Healthy", 3, 5), null, new CategoryScore(5, "Healthy", 2, 4), new CategoryScore(0, "Healthy", 1, 3)),
        FrameTime: null,
        CulpritAttribution: null,
        Recommendations: Array.Empty<RecommendationEntry>(),
        BaselineComparison: null,
        SelfOverhead: new SelfOverhead(0.5, 50000000, 1, 0.5, 0),
        TimeSeries: new TimeSeriesMetadata(30, 30.0, 1)
    );

    [Fact]
    public void FullSummary_RoundTrips()
    {
        var original = CreateFullSummary();
        var json = JsonReportGenerator.Serialize(original);
        var deserialized = JsonReportGenerator.Deserialize(json);
        deserialized.Should().NotBeNull();
        deserialized.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void SparseSummary_NullFieldsHandledCorrectly()
    {
        var original = CreateSparseSummary();
        var json = JsonReportGenerator.Serialize(original);

        // Null fields should be omitted (WhenWritingNull)
        json.Should().NotContain("\"frameTime\":");
        json.Should().NotContain("\"culpritAttribution\":");
        json.Should().NotContain("\"baselineComparison\":");

        var deserialized = JsonReportGenerator.Deserialize(json);
        deserialized.Should().NotBeNull();
        deserialized!.FrameTime.Should().BeNull();
        deserialized.CulpritAttribution.Should().BeNull();
        deserialized.BaselineComparison.Should().BeNull();
    }

    [Fact]
    public void Fixture_Deserializes_Correctly()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "schema_example.json");
        var json = File.ReadAllText(fixturePath);
        var summary = JsonReportGenerator.Deserialize(json);

        summary.Should().NotBeNull();
        summary!.Metadata.Version.Should().Be("1.0.0");
        summary.Metadata.Profile.Should().Be("gaming");
        summary.Fingerprint.CpuModel.Should().Be("AMD Ryzen 7 5800X");
        summary.Fingerprint.TotalRamGb.Should().Be(32);
        summary.Scores.Cpu.Score.Should().Be(45);
        summary.FrameTime.Should().NotBeNull();
        summary.FrameTime!.AvgFps.Should().Be(72.5);
        summary.Recommendations.Should().HaveCount(2);
        summary.BaselineComparison.Should().NotBeNull();
        summary.SelfOverhead.AvgCpuPercent.Should().Be(0.8);
    }

    [Fact]
    public void Json_UsesCamelCase()
    {
        var summary = CreateFullSummary();
        var json = JsonReportGenerator.Serialize(summary);
        json.Should().Contain("\"captureId\":");
        json.Should().Contain("\"cpuModel\":");
        json.Should().NotContain("\"CaptureId\":");
    }

    [Fact]
    public void Json_IsIndented()
    {
        var summary = CreateFullSummary();
        var json = JsonReportGenerator.Serialize(summary);
        json.Should().Contain("\n");
        json.Should().Contain("  ");
    }
}
