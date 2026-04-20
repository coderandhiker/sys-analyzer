using SysAnalyzer.Analysis;
using SysAnalyzer.Analysis.Models;
using SysAnalyzer.Config;
using Xunit;

namespace SysAnalyzer.Tests.Component;

public class RecommendationEngineTests
{
    private readonly RecommendationEngine _engine = new();

    [Fact]
    public void CpuBoundData_TriggersCpuBoundRec()
    {
        var fields = new Dictionary<string, object?>
        {
            ["frametime.has_data"] = true,
            ["frametime.cpu_bound_frame_pct"] = 50.0,
            ["frametime.avg_cpu_frame_ms"] = 20.0,
            ["frametime.avg_gpu_frame_ms"] = 8.0,
        };
        var config = MakeConfig(new RecommendationConfig
        {
            Id = "ft_cpu_bound",
            Trigger = "frametime.has_data AND frametime.cpu_bound_frame_pct > 30",
            Severity = "critical", Category = "frametime", Confidence = "high", Priority = 10,
            Title = "CPU-Bound", Body = "{frametime.cpu_bound_frame_pct}% CPU-bound frames."
        });

        var results = _engine.Evaluate(fields, config);
        Assert.Single(results);
        Assert.Equal("ft_cpu_bound", results[0].Id);
        Assert.Contains("50.0%", results[0].Body);
    }

    [Fact]
    public void CleanHealthy_ZeroRecommendations()
    {
        var fields = new Dictionary<string, object?>
        {
            ["frametime.has_data"] = true,
            ["frametime.cpu_bound_frame_pct"] = 5.0,
            ["frametime.gpu_bound_frame_pct"] = 10.0,
            ["cpu.avg_load"] = 30.0,
            ["gpu.avg_load"] = 40.0,
            ["memory.score"] = 20.0,
            ["disk.os_drive_is_hdd"] = false,
            ["system.power_plan"] = "High performance",
        };
        var config = MakeConfig(
            new RecommendationConfig
            {
                Id = "ft_cpu_bound",
                Trigger = "frametime.has_data AND frametime.cpu_bound_frame_pct > 30",
                Severity = "critical", Category = "frametime", Confidence = "high", Priority = 10,
                Title = "CPU-Bound", Body = "CPU-bound."
            },
            new RecommendationConfig
            {
                Id = "disk_hdd_os",
                Trigger = "disk.os_drive_is_hdd == true",
                Severity = "critical", Category = "disk", Confidence = "high", Priority = 10,
                Title = "HDD OS", Body = "OS on HDD."
            });

        var results = _engine.Evaluate(fields, config);
        Assert.Empty(results);
    }

    [Fact]
    public void ConfidenceAutoEscalation_WithEvidenceBoost()
    {
        var fields = new Dictionary<string, object?>
        {
            ["memory.score"] = 80.0,
            ["memory.slots_available"] = 2.0,
            ["memory.installed_sticks"] = 2.0,
            ["memory.stick_capacity_gb"] = 8.0,
            ["memory.memory_type"] = "DDR5",
            ["memory.speed_mhz"] = 6000.0,
            ["memory.total_gb"] = 16.0,
            ["frametime.has_data"] = true,
            ["frametime.stutter_correlates_with_pagefault"] = true,
        };
        var config = MakeConfig(new RecommendationConfig
        {
            Id = "mem_add_sticks",
            Trigger = "memory.score >= 70 AND memory.slots_available > 0",
            Severity = "critical", Category = "memory", Confidence = "auto", Priority = 10,
            Title = "Add RAM",
            Body = "Add more RAM. {memory.total_gb}GB total.",
            EvidenceBoost = "frametime.has_data AND frametime.stutter_correlates_with_pagefault"
        });

        var results = _engine.Evaluate(fields, config);
        Assert.Single(results);
        Assert.Equal("high", results[0].Confidence);
    }

    [Fact]
    public void MissingCulpritData_TemplateDegradesGracefully()
    {
        var fields = new Dictionary<string, object?>
        {
            ["frametime.has_data"] = true,
            ["frametime.cpu_bound_frame_pct"] = 50.0,
            // culprit.top_process not set
        };
        var config = MakeConfig(new RecommendationConfig
        {
            Id = "test_rec",
            Trigger = "frametime.has_data AND frametime.cpu_bound_frame_pct > 30",
            Severity = "warning", Category = "general", Confidence = "medium", Priority = 5,
            Title = "Test",
            Body = "Top culprit: {culprit.top_process}."
        });

        var results = _engine.Evaluate(fields, config);
        Assert.Single(results);
        Assert.DoesNotContain("{culprit.top_process}", results[0].Body);
        Assert.Contains("data unavailable", results[0].Body);
    }

    [Fact]
    public void Determinism_100Runs_IdenticalOutput()
    {
        var fields = new Dictionary<string, object?>
        {
            ["frametime.has_data"] = true,
            ["frametime.cpu_bound_frame_pct"] = 50.0,
            ["frametime.avg_cpu_frame_ms"] = 20.0,
            ["frametime.avg_gpu_frame_ms"] = 8.0,
            ["cpu.avg_load"] = 90.0,
            ["memory.score"] = 80.0,
            ["memory.slots_available"] = 2.0,
        };
        var config = MakeConfig(
            new RecommendationConfig
            {
                Id = "ft_cpu_bound",
                Trigger = "frametime.has_data AND frametime.cpu_bound_frame_pct > 30",
                Severity = "critical", Category = "frametime", Confidence = "high", Priority = 10,
                Title = "CPU-Bound", Body = "{frametime.cpu_bound_frame_pct}% CPU-bound."
            },
            new RecommendationConfig
            {
                Id = "mem_add",
                Trigger = "memory.score >= 70 AND memory.slots_available > 0",
                Severity = "critical", Category = "memory", Confidence = "medium", Priority = 10,
                Title = "Add RAM", Body = "Add RAM."
            });

        var firstRun = _engine.Evaluate(fields, config);

        for (int i = 0; i < 100; i++)
        {
            var run = _engine.Evaluate(fields, config);
            Assert.Equal(firstRun.Count, run.Count);
            for (int j = 0; j < firstRun.Count; j++)
            {
                Assert.Equal(firstRun[j].Id, run[j].Id);
                Assert.Equal(firstRun[j].Body, run[j].Body);
                Assert.Equal(firstRun[j].Confidence, run[j].Confidence);
            }
        }
    }

    [Fact]
    public void SortedByPriorityThenSeverity()
    {
        var fields = new Dictionary<string, object?>
        {
            ["frametime.has_data"] = true,
            ["frametime.cpu_bound_frame_pct"] = 50.0,
            ["disk.os_drive_is_hdd"] = true,
            ["disk.os_drive_model"] = "WD Blue",
        };
        var config = MakeConfig(
            new RecommendationConfig
            {
                Id = "low_pri", Trigger = "frametime.has_data", Severity = "info",
                Category = "general", Confidence = "high", Priority = 1, Title = "Low", Body = "Low."
            },
            new RecommendationConfig
            {
                Id = "high_pri", Trigger = "disk.os_drive_is_hdd == true", Severity = "critical",
                Category = "disk", Confidence = "high", Priority = 10, Title = "High", Body = "High."
            });

        var results = _engine.Evaluate(fields, config);
        Assert.Equal("high_pri", results[0].Id);
        Assert.Equal("low_pri", results[1].Id);
    }

    private static AnalyzerConfig MakeConfig(params RecommendationConfig[] recs)
    {
        return new AnalyzerConfig { Recommendations = [.. recs] };
    }
}
