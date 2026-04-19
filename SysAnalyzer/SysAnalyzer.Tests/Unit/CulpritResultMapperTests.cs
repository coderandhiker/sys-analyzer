using System.Text.Json;
using SysAnalyzer.Analysis;
using SysAnalyzer.Analysis.Models;
using SysAnalyzer.Report;

namespace SysAnalyzer.Tests.Unit;

public class CulpritResultMapperTests
{
    [Fact]
    public void ToSummary_NullResult_ReturnsNull()
    {
        var result = CulpritResultMapper.ToSummary(null);
        Assert.Null(result);
    }

    [Fact]
    public void ToSummary_NoAttribution_ReturnsNull()
    {
        var result = new CulpritAttributionResult(
            TopContextSwitchProcesses: [],
            TopDpcDrivers: [],
            TopDiskIoProcesses: [],
            ProcessLifetimeEvents: [],
            HasAttribution: false,
            HasDpcAttribution: false,
            InterferenceCorrelation: 0f);

        Assert.Null(CulpritResultMapper.ToSummary(result));
    }

    [Fact]
    public void ToSummary_WithData_MapsCorrectly()
    {
        var result = new CulpritAttributionResult(
            TopContextSwitchProcesses: [
                new ProcessCulprit("MsMpEng.exe", 200, 50, 68.5f, 0.8f, "Windows Defender real-time scanner", "Exclude game folder from Defender scans")
            ],
            TopDpcDrivers: [
                new DriverCulprit("ndis.sys", 12.5, 85.3f, null)
            ],
            TopDiskIoProcesses: [
                new ProcessCulprit("OneDrive.exe", 300, 0, 45.2f, 0.6f, "OneDrive cloud sync", "Pause OneDrive sync during gaming")
            ],
            ProcessLifetimeEvents: [
                new ProcessLifetimeEntry("MsMpEng.exe", 200, true, 4.9, true)
            ],
            HasAttribution: true,
            HasDpcAttribution: true,
            InterferenceCorrelation: 0.8f);

        var summary = CulpritResultMapper.ToSummary(result);

        Assert.NotNull(summary);
        Assert.True(summary!.HasAttribution);
        Assert.True(summary.HasDpcAttribution);
        Assert.Single(summary.TopProcesses);
        Assert.Equal("MsMpEng.exe", summary.TopProcesses[0].Name);
        Assert.Equal(68.5, summary.TopProcesses[0].ContextSwitchPct);
        Assert.Single(summary.TopDpcDrivers);
        Assert.Equal("ndis.sys", summary.TopDpcDrivers[0].Module);
        Assert.Single(summary.TopDiskProcesses);
        Assert.Equal("OneDrive.exe", summary.TopDiskProcesses[0].Name);
        Assert.NotNull(summary.ProcessLifetimeEvents);
        Assert.Single(summary.ProcessLifetimeEvents);
    }

    [Fact]
    public void PopulateFlatDictionary_NoAttribution_SetsHasAttributionFalse()
    {
        var dict = new Dictionary<string, object>();
        CulpritResultMapper.PopulateFlatDictionary(dict, null);

        Assert.Equal(false, dict["culprit.has_attribution"]);
        Assert.Equal(false, dict["culprit.has_dpc_attribution"]);
    }

    [Fact]
    public void PopulateFlatDictionary_WithData_PopulatesAllFields()
    {
        var result = new CulpritAttributionResult(
            TopContextSwitchProcesses: [
                new ProcessCulprit("MsMpEng.exe", 200, 50, 68.5f, 0.8f, "Windows Defender real-time scanner", "Exclude game folder from Defender scans")
            ],
            TopDpcDrivers: [
                new DriverCulprit("ndis.sys", 12.5, 85.3f, null)
            ],
            TopDiskIoProcesses: [
                new ProcessCulprit("OneDrive.exe", 300, 0, 45.2f, 0.6f, "OneDrive cloud sync", "Pause OneDrive sync during gaming")
            ],
            ProcessLifetimeEvents: [],
            HasAttribution: true,
            HasDpcAttribution: true,
            InterferenceCorrelation: 0.8f);

        var dict = new Dictionary<string, object>();
        CulpritResultMapper.PopulateFlatDictionary(dict, result);

        Assert.Equal(true, dict["culprit.has_attribution"]);
        Assert.Equal(true, dict["culprit.has_dpc_attribution"]);
        Assert.Equal("MsMpEng.exe", dict["culprit.top_process_name"]);
        Assert.Equal(68.5, (double)dict["culprit.top_process_ctx_switch_pct"], 1);
        Assert.Equal("Windows Defender real-time scanner", dict["culprit.top_process_description"]);
        Assert.Equal("Exclude game folder from Defender scans", dict["culprit.top_process_remediation"]);
        Assert.Equal("ndis.sys", dict["culprit.top_dpc_driver"]);
        Assert.Equal(85.3, (double)dict["culprit.top_dpc_driver_pct"], 1);
        Assert.Equal("OneDrive.exe", dict["culprit.disk_io_top_process"]);
        Assert.InRange((double)dict["culprit.interference_correlation"], 0.7, 0.9);
    }

    [Fact]
    public void CulpritAttribution_SerializesToJson_WithCulpritsArray()
    {
        var culprit = new CulpritAttribution(
            TopProcesses: [
                new ProcessEntry("MsMpEng.exe", 68.5, "Windows Defender real-time scanner", "Exclude game folder", 0.8)
            ],
            TopDpcDrivers: [
                new DpcDriverEntry("ndis.sys", 85.3, null)
            ],
            TopDiskProcesses: [
                new DiskProcessEntry("OneDrive.exe", 45.2, "OneDrive cloud sync", "Pause sync", 0.6)
            ],
            HasAttribution: true,
            HasDpcAttribution: true,
            InterferenceCorrelation: 0.8f);

        var json = JsonSerializer.Serialize(culprit, JsonReportGenerator.Options);

        Assert.Contains("\"topProcesses\"", json);
        Assert.Contains("\"MsMpEng.exe\"", json);
        Assert.Contains("\"topDpcDrivers\"", json);
        Assert.Contains("\"ndis.sys\"", json);
        Assert.Contains("\"topDiskProcesses\"", json);
        Assert.Contains("\"hasAttribution\"", json);
        Assert.Contains("\"hasDpcAttribution\"", json);
        Assert.Contains("\"interferenceCorrelation\"", json);
    }

    [Fact]
    public void CulpritAttribution_Null_SerializedAsNull()
    {
        // Verify that when CulpritAttribution is null, the JSON key is absent
        // (because DefaultIgnoreCondition = WhenWritingNull)
        var summary = CreateMinimalSummary(culprit: null);
        var json = JsonReportGenerator.Serialize(summary);

        Assert.DoesNotContain("\"culpritAttribution\"", json);
    }

    [Fact]
    public void CulpritAttribution_Present_IncludedInJson()
    {
        var culprit = new CulpritAttribution(
            TopProcesses: [new ProcessEntry("dwm.exe", 12.5, "Desktop Window Manager")],
            TopDpcDrivers: [],
            TopDiskProcesses: [],
            HasAttribution: true);

        var summary = CreateMinimalSummary(culprit: culprit);
        var json = JsonReportGenerator.Serialize(summary);

        Assert.Contains("\"culpritAttribution\"", json);
        Assert.Contains("\"topProcesses\"", json);
        Assert.Contains("\"dwm.exe\"", json);
    }

    private static AnalysisSummary CreateMinimalSummary(CulpritAttribution? culprit = null)
    {
        return new AnalysisSummary(
            Metadata: new AnalysisMetadata("1.0.0", DateTime.UtcNow, 30, null, "gaming", "Tier1", "test-cap"),
            Fingerprint: new MachineFingerprint("CPU", "GPU", 16, "2x8GB", "22631", "1920x1080", "abc123", "537.70", "Board"),
            SensorHealth: new SensorHealthSummary("Tier1", []),
            HardwareInventory: new HardwareInventorySummary("CPU", 4, 8, 3000, 4000, 8388608, [], 16, 4, 2, null, null, null, [], "Board", "1.0", "2024-01-01", "22631", "Win11", [], "1920x1080", 60),
            SystemConfiguration: new SystemConfigurationSummary("Balanced", false, false, false, false, false, 0, 0, "System managed", 0, "None"),
            Scores: new ScoresSummary(
                new CategoryScore(50, "Moderate", 6, 6),
                new CategoryScore(30, "Healthy", 5, 5),
                null,
                new CategoryScore(15, "Healthy", 4, 4),
                new CategoryScore(5, "Healthy", 3, 3)),
            FrameTime: null,
            CulpritAttribution: culprit,
            Recommendations: [],
            BaselineComparison: null,
            SelfOverhead: new SelfOverhead(0.5, 50_000_000, 1, 0.5, 0),
            TimeSeries: new TimeSeriesMetadata(30, 30, 1));
    }
}
