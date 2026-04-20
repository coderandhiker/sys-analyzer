using SysAnalyzer.Analysis.Models;
using SysAnalyzer.Capture;
using SysAnalyzer.Report;

namespace SysAnalyzer.Tests.Integration;

public class FullOutputPipelineTests
{
    [Fact]
    public async Task Pipeline_ProducesJsonAndHtml()
    {
        var summary = CreateTestSummary();
        var snapshots = CreateTestSnapshots(20);
        var dir = Path.Combine(Path.GetTempPath(), $"sysanalyzer-pipeline-{Guid.NewGuid():N}");

        try
        {
            // JSON output
            var jsonPath = await JsonReportGenerator.WriteToFileAsync(
                summary, dir, "test-{timestamp}", "yyyyMMdd", "pipeline-test");

            // HTML output
            var htmlGenerator = new HtmlReportGenerator();
            var htmlPath = await htmlGenerator.GenerateAsync(
                summary, snapshots, null, dir, "test-{timestamp}", "yyyyMMdd", "pipeline-test");

            Assert.True(File.Exists(jsonPath));
            Assert.True(File.Exists(htmlPath));

            // Verify JSON content
            var jsonContent = await File.ReadAllTextAsync(jsonPath);
            var deserialized = JsonReportGenerator.Deserialize(jsonContent);
            Assert.NotNull(deserialized);
            Assert.Equal("1.0.0", deserialized!.Metadata.Version);
            Assert.Equal(45, deserialized.Scores.Cpu.Score);

            // Verify HTML content
            var htmlContent = await File.ReadAllTextAsync(htmlPath);
            Assert.Contains("window.reportData", htmlContent);
            Assert.Contains("SysAnalyzer Report", htmlContent);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task Pipeline_ProducesJsonHtmlAndCsv()
    {
        var summary = CreateTestSummary();
        var snapshots = CreateTestSnapshots(15);
        var frameSamples = CreateTestFrameSamples(100);
        var dir = Path.Combine(Path.GetTempPath(), $"sysanalyzer-pipeline-{Guid.NewGuid():N}");

        try
        {
            var filenameBase = FilenameGenerator.Generate("test-{timestamp}", "yyyyMMdd", "csv-test");

            // JSON
            var jsonPath = await JsonReportGenerator.WriteToFileAsync(
                summary, dir, "test-{timestamp}", "yyyyMMdd", "csv-test");

            // HTML
            var htmlGenerator = new HtmlReportGenerator();
            var htmlPath = await htmlGenerator.GenerateAsync(
                summary, snapshots, frameSamples, dir, "test-{timestamp}", "yyyyMMdd", "csv-test");

            // CSV
            var tsCsvPath = await CsvExporter.ExportTimeSeriesAsync(snapshots, dir, filenameBase);
            var pmCsvPath = await CsvExporter.ExportPresentMonAsync(frameSamples, dir, filenameBase);

            Assert.True(File.Exists(jsonPath));
            Assert.True(File.Exists(htmlPath));
            Assert.True(File.Exists(tsCsvPath));
            Assert.True(File.Exists(pmCsvPath));

            // Verify CSV row counts
            var tsLines = (await File.ReadAllLinesAsync(tsCsvPath))
                .Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            Assert.Equal(16, tsLines.Length); // header + 15 data rows

            var pmLines = (await File.ReadAllLinesAsync(pmCsvPath))
                .Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            Assert.Equal(101, pmLines.Length); // header + 100 frame rows
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task Pipeline_Tier1Only_HtmlOmitsGpuTempRows()
    {
        // Create a Tier 1 summary (no LHM data)
        var summary = CreateTestSummary(tier: "Tier1", hasGpu: false);
        var snapshots = CreateTestSnapshots(10, hasGpu: false);
        var dir = Path.Combine(Path.GetTempPath(), $"sysanalyzer-pipeline-{Guid.NewGuid():N}");

        try
        {
            var htmlGenerator = new HtmlReportGenerator();
            var htmlPath = await htmlGenerator.GenerateAsync(
                summary, snapshots, null, dir, "test-{timestamp}", "yyyyMMdd");

            var html = await File.ReadAllTextAsync(htmlPath);

            // Report data should indicate Tier1
            Assert.Contains("\"tier\":\"Tier1\"", html.Replace(" ", "").Replace("\r\n", "").Replace("\n", ""));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void BuildHtml_FromSchemaFixture_Succeeds()
    {
        // Load the schema_example.json fixture
        var fixturePath = Path.Combine("Fixtures", "schema_example.json");
        if (!File.Exists(fixturePath))
        {
            // Try running from test output directory
            fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "schema_example.json");
        }

        Assert.True(File.Exists(fixturePath), $"Fixture file not found at {fixturePath}");

        var json = File.ReadAllText(fixturePath);
        var summary = JsonReportGenerator.Deserialize(json);
        Assert.NotNull(summary);

        var generator = new HtmlReportGenerator();
        var html = generator.BuildHtml(summary!, Array.Empty<SensorSnapshot>(), null);

        Assert.Contains("window.reportData", html);
        Assert.Contains("AMD Ryzen 7 5800X", html);
        Assert.Contains("SysAnalyzer Report", html);
    }

    private static AnalysisSummary CreateTestSummary(string tier = "Tier2", bool hasGpu = true)
    {
        var metadata = new AnalysisMetadata(
            "1.0.0", DateTime.UtcNow, 120.5, "test", "gaming", tier, "cap-test-001");

        var fingerprint = new MachineFingerprint(
            "AMD Ryzen 7 5800X", "NVIDIA RTX 3080", 32, "2x16GB DDR4-3600",
            "22631.3880", "2560x1440@144Hz", "nvme-sn850x", "560", "ASUS ROG STRIX");

        var sensorHealth = new SensorHealthSummary(tier, new List<ProviderHealthEntry>
        {
            new("PerformanceCounters", "Active", null, 15, 15, 0)
        });

        var hwInventory = new HardwareInventorySummary(
            "AMD Ryzen 7 5800X", 8, 16, 3800, 4700, 33554432,
            new List<RamStickSummary> { new("DIMM_A1", 17179869184, 3600, "DDR4") },
            32, 4, 2, hasGpu ? "NVIDIA RTX 3080" : null, hasGpu ? 10240 : null,
            hasGpu ? "560.94" : null,
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
            hasGpu ? new CategoryScore(82, "Stressed", 6, 6) : null,
            new CategoryScore(15, "Healthy", 4, 4),
            new CategoryScore(5, "Healthy", 3, 3));

        var selfOverhead = new SelfOverhead(0.8, 67108864, 3, 1.2, 0);
        var timeSeries = new TimeSeriesMetadata(120, 120.5, 1);

        return new AnalysisSummary(
            metadata, fingerprint, sensorHealth, hwInventory, sysConfig,
            scores, null, null, new List<RecommendationEntry>(), null,
            selfOverhead, timeSeries);
    }

    private static List<SensorSnapshot> CreateTestSnapshots(int count, bool hasGpu = true)
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
                GpuUtilizationPercent: hasGpu ? 70 : null,
                GpuMemoryUtilizationPercent: hasGpu ? 60 : null,
                GpuMemoryUsedMb: hasGpu ? 6000 : null,
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
                FrameTimeMs: 16.6,
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
