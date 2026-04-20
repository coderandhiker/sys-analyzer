using SysAnalyzer.Analysis;
using SysAnalyzer.Analysis.Models;
using Xunit;

namespace SysAnalyzer.Tests.Component;

public class BaselineComparatorTests
{
    private readonly BaselineComparator _comparator = new();

    [Fact]
    public void NullBaseline_ReturnsNull()
    {
        var current = MakeSummary("id1", cpuScore: 50);
        Assert.Null(_comparator.Compare(current, null));
    }

    [Fact]
    public void SameFingerprint_FingerprintMatchTrue()
    {
        var current = MakeSummary("id1", cpuScore: 50);
        var baseline = MakeSummary("id0", cpuScore: 70);
        var result = _comparator.Compare(current, baseline);

        Assert.NotNull(result);
        Assert.True(result.FingerprintMatch);
    }

    [Fact]
    public void DifferentFingerprint_WarningEmitted()
    {
        var current = MakeSummary("id1", cpuScore: 50, ramGb: 32);
        var baseline = MakeSummary("id0", cpuScore: 70, ramGb: 16);
        var result = _comparator.Compare(current, baseline);

        Assert.NotNull(result);
        Assert.False(result.FingerprintMatch);
        Assert.NotEmpty(result.HardwareChanges);
    }

    [Fact]
    public void ScoreImproved_VerdictBetter()
    {
        var current = MakeSummary("id1", cpuScore: 23);
        var baseline = MakeSummary("id0", cpuScore: 50);
        var result = _comparator.Compare(current, baseline);

        Assert.NotNull(result);
        var cpuDelta = result.ScoreDeltas.First(d => d.Subsystem == "CPU");
        Assert.Equal("Better", cpuDelta.Verdict);
    }

    [Fact]
    public void ScoreMajorImprovement_VerdictFixed()
    {
        var current = MakeSummary("id1", cpuScore: 23);
        var baseline = MakeSummary("id0", cpuScore: 91);
        var result = _comparator.Compare(current, baseline);

        Assert.NotNull(result);
        var cpuDelta = result.ScoreDeltas.First(d => d.Subsystem == "CPU");
        Assert.Equal("Fixed", cpuDelta.Verdict);
    }

    [Fact]
    public void ScoreWorsened_VerdictWorse()
    {
        var current = MakeSummary("id1", cpuScore: 70);
        var baseline = MakeSummary("id0", cpuScore: 40);
        var result = _comparator.Compare(current, baseline);

        Assert.NotNull(result);
        var cpuDelta = result.ScoreDeltas.First(d => d.Subsystem == "CPU");
        Assert.Equal("Worse", cpuDelta.Verdict);
    }

    [Fact]
    public void ScoreMajorRegression_VerdictRegressed()
    {
        var current = MakeSummary("id1", cpuScore: 88);
        var baseline = MakeSummary("id0", cpuScore: 23);
        var result = _comparator.Compare(current, baseline);

        Assert.NotNull(result);
        var cpuDelta = result.ScoreDeltas.First(d => d.Subsystem == "CPU");
        Assert.Equal("Regressed", cpuDelta.Verdict);
    }

    [Fact]
    public void ScoreWithin5Pct_VerdictSame()
    {
        var current = MakeSummary("id1", cpuScore: 51);
        var baseline = MakeSummary("id0", cpuScore: 50);
        var result = _comparator.Compare(current, baseline);

        Assert.NotNull(result);
        var cpuDelta = result.ScoreDeltas.First(d => d.Subsystem == "CPU");
        Assert.Equal("Same", cpuDelta.Verdict);
    }

    [Fact]
    public void NewRecommendations_Tracked()
    {
        var current = MakeSummary("id1", cpuScore: 50, recIds: ["rec_a", "rec_b"]);
        var baseline = MakeSummary("id0", cpuScore: 50, recIds: ["rec_a", "rec_c"]);
        var result = _comparator.Compare(current, baseline);

        Assert.NotNull(result);
        Assert.Contains("rec_b", result.NewRecommendations);
        Assert.Contains("rec_c", result.ResolvedRecommendations);
    }

    private static AnalysisSummary MakeSummary(string captureId, int cpuScore = 50, int ramGb = 16, string[]? recIds = null)
    {
        var fingerprint = new MachineFingerprint(
            "TestCPU", "TestGPU", ramGb, "2x8GB DDR5-6000",
            "26100.1", "1920x1080@144Hz", "nvme_hash", "537", "TestMobo");

        var scores = new ScoresSummary(
            new CategoryScore(cpuScore, "Moderate", 6, 6),
            new CategoryScore(50, "Moderate", 5, 5),
            new CategoryScore(40, "Moderate", 6, 6),
            new CategoryScore(20, "Healthy", 4, 4),
            new CategoryScore(10, "Healthy", 3, 3));

        var recs = (recIds ?? []).Select(id => new RecommendationEntry(
            id, "Title", "Body", "warning", "general", "medium", 5, [])).ToList();

        return new AnalysisSummary(
            new AnalysisMetadata("1.0", DateTime.UtcNow, 60, null, "gaming", "Tier2", captureId),
            fingerprint,
            new SensorHealthSummary("Tier2", []),
            MakeHardwareInventory(ramGb),
            MakeSystemConfig(),
            scores,
            null, null,
            recs,
            null,
            new SelfOverhead(0.5, 50_000_000, 3, 1.2, 0),
            new TimeSeriesMetadata(60, 60, 1));
    }

    private static HardwareInventorySummary MakeHardwareInventory(int ramGb) => new(
        "TestCPU", 8, 16, 3600, 5200, 32_000_000,
        [new RamStickSummary("BANK0", (long)ramGb / 2 * 1024 * 1024 * 1024, 6000, "DDR5")],
        ramGb, 4, 2, "TestGPU", 8192, "537.58",
        [new DiskDriveSummary("NVMe SSD", "NVMe", 1_000_000_000_000)],
        "TestMobo", "1.0", "2024-01-01",
        "26100.1", "Windows 11",
        [new NetworkAdapterSummary("Ethernet", 1_000_000_000)],
        "1920x1080", 144);

    private static SystemConfigurationSummary MakeSystemConfig() => new(
        "High performance", true, true, false, false, false, 1024, 512, "System managed", 10, "Windows Defender");
}
