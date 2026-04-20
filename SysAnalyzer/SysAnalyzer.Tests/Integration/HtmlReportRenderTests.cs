using System.Text.Json;
using SysAnalyzer.Analysis.Models;
using SysAnalyzer.Capture;
using SysAnalyzer.Report;

namespace SysAnalyzer.Tests.Integration;

public class HtmlReportRenderTests
{
    [Fact]
    public async Task GenerateAsync_ProducesHtmlFile()
    {
        var summary = CreateTestSummary();
        var snapshots = CreateTestSnapshots(10);
        var dir = Path.Combine(Path.GetTempPath(), $"sysanalyzer-html-{Guid.NewGuid():N}");

        try
        {
            var generator = new HtmlReportGenerator();
            var path = await generator.GenerateAsync(
                summary, snapshots, null, dir, "test-{timestamp}", "yyyyMMdd", "test-label");

            Assert.True(File.Exists(path));
            Assert.EndsWith(".html", path);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void BuildHtml_ContainsTablerCss()
    {
        var summary = CreateTestSummary();
        var generator = new HtmlReportGenerator();

        var html = generator.BuildHtml(summary, Array.Empty<SensorSnapshot>(), null);

        // Tabler CSS should be inlined
        Assert.Contains("--tblr", html);
        Assert.Contains(".card", html);
    }

    [Fact]
    public void BuildHtml_ContainsApexChartsJs()
    {
        var summary = CreateTestSummary();
        var generator = new HtmlReportGenerator();

        var html = generator.BuildHtml(summary, Array.Empty<SensorSnapshot>(), null);

        Assert.Contains("ApexCharts", html);
    }

    [Fact]
    public void BuildHtml_ContainsReportData()
    {
        var summary = CreateTestSummary();
        var generator = new HtmlReportGenerator();

        var html = generator.BuildHtml(summary, Array.Empty<SensorSnapshot>(), null);

        Assert.Contains("window.reportData", html);
        Assert.Contains("\"captureId\"", html);
        Assert.Contains("\"scores\"", html);
    }

    [Fact]
    public void BuildHtml_NoExternalResourceLoads()
    {
        var summary = CreateTestSummary();
        var generator = new HtmlReportGenerator();

        var html = generator.BuildHtml(summary, CreateTestSnapshots(5), null);

        // Should not contain link/script tags that load external resources
        // (URLs in JS comments/strings from inlined libraries are acceptable)
        Assert.DoesNotContain("<link rel=\"stylesheet\" href=\"http", html);
        Assert.DoesNotContain("<script src=\"http", html);
        Assert.DoesNotContain("//cdn.", html);
    }

    [Fact]
    public void BuildHtml_SelfContained_NoPlaceholdersLeft()
    {
        var summary = CreateTestSummary();
        var generator = new HtmlReportGenerator();

        var html = generator.BuildHtml(summary, CreateTestSnapshots(5), null);

        Assert.DoesNotContain("TABLER_CSS_PLACEHOLDER", html);
        Assert.DoesNotContain("REPORT_CSS_PLACEHOLDER", html);
        Assert.DoesNotContain("TABLER_THEME_JS_PLACEHOLDER", html);
        Assert.DoesNotContain("APEXCHARTS_JS_PLACEHOLDER", html);
        Assert.DoesNotContain("TABLER_JS_PLACEHOLDER", html);
        Assert.DoesNotContain("REPORT_DATA_PLACEHOLDER", html);
    }

    [Fact]
    public void BuildHtml_WithFrameTimeSummary_ContainsFrameTimeSection()
    {
        var summary = CreateTestSummary(includeFrameTime: true);
        var generator = new HtmlReportGenerator();
        var frameSamples = CreateTestFrameSamples(50);

        var html = generator.BuildHtml(summary, CreateTestSnapshots(10), frameSamples);

        Assert.Contains("\"frameTime\"", html);
        Assert.Contains("frameTimeOverTime", html);
    }

    [Fact]
    public void BuildHtml_WithRecommendations_ContainsRecommendationData()
    {
        var summary = CreateTestSummary(includeRecommendations: true);
        var generator = new HtmlReportGenerator();

        var html = generator.BuildHtml(summary, Array.Empty<SensorSnapshot>(), null);

        Assert.Contains("GPU-Bound Rendering", html);
        Assert.Contains("\"confidence\"", html);
    }

    [Fact]
    public void BuildHtml_WithCulprits_ContainsCulpritData()
    {
        var summary = CreateTestSummary(includeCulprits: true);
        var generator = new HtmlReportGenerator();

        var html = generator.BuildHtml(summary, Array.Empty<SensorSnapshot>(), null);

        Assert.Contains("dwm.exe", html);
    }

    [Fact]
    public void BuildHtml_WithBaselineComparison_ContainsBaselineData()
    {
        var summary = CreateTestSummary(includeBaseline: true);
        var generator = new HtmlReportGenerator();

        var html = generator.BuildHtml(summary, Array.Empty<SensorSnapshot>(), null);

        Assert.Contains("baselineComparison", html);
        Assert.Contains("cpu.avg_load", html);
    }

    private static AnalysisSummary CreateTestSummary(
        bool includeFrameTime = false,
        bool includeRecommendations = false,
        bool includeCulprits = false,
        bool includeBaseline = false)
    {
        var metadata = new AnalysisMetadata(
            "1.0.0", DateTime.UtcNow, 120.5, "test-label", "gaming", "Tier2", "cap-test-001");

        var fingerprint = new MachineFingerprint(
            "AMD Ryzen 7 5800X", "NVIDIA RTX 3080", 32, "2x16GB DDR4-3600",
            "22631.3880", "2560x1440@144Hz", "nvme-sn850x", "560", "ASUS ROG STRIX");

        var sensorHealth = new SensorHealthSummary("Tier2", new List<ProviderHealthEntry>
        {
            new("PerformanceCounters", "Active", null, 15, 15, 0),
            new("LibreHardwareMonitor", "Active", null, 7, 7, 0)
        });

        var hwInventory = new HardwareInventorySummary(
            "AMD Ryzen 7 5800X", 8, 16, 3800, 4700, 33554432,
            new List<RamStickSummary> { new("DIMM_A1", 17179869184, 3600, "DDR4") },
            32, 4, 2, "NVIDIA RTX 3080", 10240, "560.94",
            new List<DiskDriveSummary> { new("WD_BLACK SN850X", "NVMe", 1000204886016) },
            "ASUS ROG STRIX", "2803", "2024-01-15", "22631.3880", "Windows 11 23H2",
            new List<NetworkAdapterSummary> { new("Intel I225-V", 2500000000) },
            "2560x1440", 144);

        var sysConfig = new SystemConfigurationSummary(
            "High performance", true, true, false, false, true,
            450, 1200, "System managed", 12, "Windows Defender");

        var scores = new ScoresSummary(
            new CategoryScore(45, "Moderate", 6, 6),
            new CategoryScore(30, "Healthy", 5, 5),
            new CategoryScore(82, "Stressed", 6, 6),
            new CategoryScore(15, "Healthy", 4, 4),
            new CategoryScore(5, "Healthy", 3, 3));

        FrameTimeSummary? frameTime = includeFrameTime
            ? new FrameTimeSummary(true, "TestApp.exe", 72.5, 30.0, 13.8, 18.2, 28.5, 45.1,
                0.3, 15.2, 78.5, "Hardware: Independent Flip", true, 7, null)
            : null;

        CulpritAttribution? culprits = includeCulprits
            ? new CulpritAttribution(
                new List<ProcessEntry> { new("dwm.exe", 12.5, "Desktop Window Manager") },
                new List<DpcDriverEntry> { new("ndis.sys", 2.1) },
                new List<DiskProcessEntry> { new("SearchIndexer.exe", 35.0) })
            : null;

        var recommendations = includeRecommendations
            ? new List<RecommendationEntry>
            {
                new("ft_gpu_bound", "GPU-Bound Rendering",
                    "78.5% of frames were GPU-bound.", "warning", "frametime",
                    "high", 8, new List<string> { "gpu_bound=78.5" })
            }
            : new List<RecommendationEntry>();

        BaselineComparisonSummary? baseline = includeBaseline
            ? new BaselineComparisonSummary("cap-baseline-001", true,
                new List<DeltaEntry> { new("cpu.avg_load", 52.3, 45.0, -7.3) })
            : null;

        var selfOverhead = new SelfOverhead(0.8, 67108864, 3, 1.2, 0);
        var timeSeries = new TimeSeriesMetadata(120, 120.5, 1);

        return new AnalysisSummary(
            metadata, fingerprint, sensorHealth, hwInventory, sysConfig,
            scores, frameTime, culprits, recommendations, baseline,
            selfOverhead, timeSeries);
    }

    private static List<SensorSnapshot> CreateTestSnapshots(int count)
    {
        var snapshots = new List<SensorSnapshot>();
        for (int i = 0; i < count; i++)
        {
            snapshots.Add(new SensorSnapshot(
                Timestamp: new QpcTimestamp((long)(i * QpcTimestamp.Frequency)),
                TotalCpuPercent: 30 + i * 0.5,
                PerCoreCpuPercent: new[] { 25.0, 35.0, 20.0, 40.0 },
                ContextSwitchesPerSec: 1000,
                DpcTimePercent: 1.5,
                InterruptsPerSec: 500,
                MemoryUtilizationPercent: 45,
                AvailableMemoryMb: 16000,
                PageFaultsPerSec: 200,
                HardFaultsPerSec: 10,
                CommittedBytes: 8000000000,
                CommittedBytesInUsePercent: 50,
                GpuUtilizationPercent: 70,
                GpuMemoryUtilizationPercent: 60,
                GpuMemoryUsedMb: 6000,
                DiskActiveTimePercent: 20,
                DiskQueueLength: 1.5,
                DiskBytesPerSec: 50000000,
                DiskReadLatencyMs: 2.5,
                DiskWriteLatencyMs: 3.0,
                NetworkBytesPerSec: 1000000,
                NetworkUtilizationPercent: 5,
                TcpRetransmitsPerSec: 0.1,
                CpuTempC: null,
                CpuClockMhz: null,
                CpuPowerW: null,
                GpuTempC: null,
                GpuClockMhz: null,
                GpuPowerW: null,
                GpuFanRpm: null
            ));
        }
        return snapshots;
    }

    private static List<FrameTimeSample> CreateTestFrameSamples(int count)
    {
        var samples = new List<FrameTimeSample>();
        for (int i = 0; i < count; i++)
        {
            samples.Add(new FrameTimeSample(
                Timestamp: new QpcTimestamp((long)(i * (QpcTimestamp.Frequency / 60.0))),
                ApplicationName: "TestApp.exe",
                FrameTimeMs: 16.6 + (i % 10 == 0 ? 20.0 : 0),
                CpuBusyMs: 8.0,
                GpuBusyMs: 12.0,
                Dropped: false,
                PresentMode: "Hardware: Independent Flip",
                AllowsTearing: true
            ));
        }
        return samples;
    }
}
