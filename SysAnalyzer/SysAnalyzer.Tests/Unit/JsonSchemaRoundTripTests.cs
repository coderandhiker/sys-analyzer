using System.Text.Json;
using SysAnalyzer.Analysis.Models;
using SysAnalyzer.Report;

namespace SysAnalyzer.Tests.Unit;

public class JsonSchemaRoundTripTests
{
    [Fact]
    public void RoundTrip_FixtureFile_DeserializesAndReserializes()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "schema_example.json");
        var json = File.ReadAllText(fixturePath);
        var summary = JsonReportGenerator.Deserialize(json);
        Assert.NotNull(summary);

        var reserialized = JsonReportGenerator.Serialize(summary!);
        var roundTripped = JsonReportGenerator.Deserialize(reserialized);
        Assert.NotNull(roundTripped);

        Assert.Equal(summary!.Metadata.Version, roundTripped!.Metadata.Version);
        Assert.Equal(summary.Metadata.CaptureId, roundTripped.Metadata.CaptureId);
        Assert.Equal(summary.Fingerprint.CpuModel, roundTripped.Fingerprint.CpuModel);
        Assert.Equal(summary.Fingerprint.GpuModel, roundTripped.Fingerprint.GpuModel);
        Assert.Equal(summary.Fingerprint.TotalRamGb, roundTripped.Fingerprint.TotalRamGb);
    }

    [Fact]
    public void Deserialize_Fixture_AllSectionsPresent()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "schema_example.json");
        var json = File.ReadAllText(fixturePath);
        var summary = JsonReportGenerator.Deserialize(json);
        Assert.NotNull(summary);

        Assert.NotNull(summary!.Metadata);
        Assert.NotNull(summary.Fingerprint);
        Assert.NotNull(summary.SensorHealth);
        Assert.NotNull(summary.HardwareInventory);
        Assert.NotNull(summary.SystemConfiguration);
        Assert.NotNull(summary.Scores);
        Assert.NotNull(summary.FrameTime);
        Assert.NotNull(summary.CulpritAttribution);
        Assert.NotNull(summary.Recommendations);
        Assert.NotNull(summary.SelfOverhead);
        Assert.NotNull(summary.TimeSeries);
    }

    [Fact]
    public void Serialize_UsesCamelCase()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "schema_example.json");
        var json = File.ReadAllText(fixturePath);
        var summary = JsonReportGenerator.Deserialize(json);
        var reserialized = JsonReportGenerator.Serialize(summary!);

        Assert.Contains("\"cpuModel\"", reserialized);
        Assert.Contains("\"gpuModel\"", reserialized);
        Assert.DoesNotContain("\"CpuModel\"", reserialized);
    }

    [Fact]
    public void Serialize_IsIndented()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "schema_example.json");
        var json = File.ReadAllText(fixturePath);
        var summary = JsonReportGenerator.Deserialize(json);
        var reserialized = JsonReportGenerator.Serialize(summary!);

        Assert.Contains("\n", reserialized);
        Assert.Contains("  ", reserialized);
    }

    [Fact]
    public void Deserialize_Fixture_MetadataValues()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "schema_example.json");
        var json = File.ReadAllText(fixturePath);
        var summary = JsonReportGenerator.Deserialize(json)!;

        Assert.Equal("1.0.0", summary.Metadata.Version);
        Assert.Equal("gaming", summary.Metadata.Profile);
        Assert.Equal("cyberpunk-ultra-4k", summary.Metadata.Label);
        Assert.Equal(120.5, summary.Metadata.DurationSeconds);
    }

    [Fact]
    public void Deserialize_Fixture_SensorHealthProviders()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "schema_example.json");
        var json = File.ReadAllText(fixturePath);
        var summary = JsonReportGenerator.Deserialize(json)!;

        Assert.Equal(4, summary.SensorHealth.Providers.Count);
        Assert.Contains(summary.SensorHealth.Providers, p => p.Name == "PerformanceCounters");
        var etwProvider = Assert.Single(summary.SensorHealth.Providers, p => p.Name == "ETW");
        Assert.NotNull(etwProvider.DegradationReason);
    }

    [Fact]
    public void Deserialize_Fixture_Recommendations()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "schema_example.json");
        var json = File.ReadAllText(fixturePath);
        var summary = JsonReportGenerator.Deserialize(json)!;

        Assert.Equal(2, summary.Recommendations.Count);
        Assert.Contains(summary.Recommendations, r => r.Id == "ft_gpu_bound");
        Assert.Contains(summary.Recommendations, r => r.Id == "sw_search_indexer");
    }
}
