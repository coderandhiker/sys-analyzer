using SysAnalyzer.Analysis.Models;
using SysAnalyzer.Config;
using SysAnalyzer.Config.ExpressionEngine;

namespace SysAnalyzer.Analysis;

/// <summary>
/// Evaluates recommendation triggers against analysis data and produces sorted recommendation list (§6.2).
/// Deterministic: same input + same config → identical output.
/// </summary>
public class RecommendationEngine
{
    private static readonly string DataUnavailableMessage = "[data unavailable — run with ETW enabled]";

    public List<RecommendationEntry> Evaluate(
        Dictionary<string, object?> analysisFields,
        AnalyzerConfig config)
    {
        var results = new List<RecommendationEntry>();

        foreach (var rec in config.Recommendations)
        {
            try
            {
                var ast = ExpressionParser.Parse(rec.Trigger);
                var evaluator = new ExpressionEvaluator(analysisFields);

                if (!evaluator.Evaluate(ast))
                    continue;

                string confidence = ResolveConfidence(rec, ast, analysisFields);
                string body = ResolveBody(rec.Body, analysisFields);
                var evidence = CollectEvidence(ast, analysisFields);

                results.Add(new RecommendationEntry(
                    rec.Id, rec.Title, body, rec.Severity, rec.Category,
                    confidence, rec.Priority, evidence));
            }
            catch
            {
                // Skip malformed triggers
            }
        }

        // Sort: priority desc, then severity order (critical > warning > info)
        results.Sort((a, b) =>
        {
            int priorityCmp = b.Priority.CompareTo(a.Priority);
            if (priorityCmp != 0) return priorityCmp;
            return SeverityOrder(b.Severity).CompareTo(SeverityOrder(a.Severity));
        });

        // Group: high confidence first, then medium, then low
        results = [
            .. results.Where(r => r.Confidence == "high"),
            .. results.Where(r => r.Confidence == "medium"),
            .. results.Where(r => r.Confidence == "low"),
        ];

        return results;
    }

    /// <summary>
    /// Build flat analysis dictionary from all data sources.
    /// </summary>
    public static Dictionary<string, object?> BuildFieldDictionary(
        AggregatedMetrics? metrics,
        FrameTimeSummary? frameTime,
        CulpritAttributionResult? culprits,
        ScoringResult? scores,
        FrameTimeCorrelation? correlation,
        CrossCorrelationResult? crossCorrelation,
        Capture.HardwareInventory? hardware,
        Capture.SystemConfiguration? sysConfig)
    {
        var fields = new Dictionary<string, object?>();

        // Frame-time fields
        if (frameTime is not null)
        {
            fields["frametime.has_data"] = frameTime.Available;
            fields["frametime.avg_fps"] = frameTime.AvgFps;
            fields["frametime.p1_fps"] = frameTime.P1Fps;
            fields["frametime.p99_ms"] = frameTime.P99FrameTimeMs;
            fields["frametime.p999_ms"] = frameTime.P999FrameTimeMs;
            fields["frametime.stutter_count"] = (double)frameTime.StutterCount;
            fields["frametime.dropped_pct"] = frameTime.DroppedFramePct;
            fields["frametime.cpu_bound_frame_pct"] = frameTime.CpuBoundPct;
            fields["frametime.gpu_bound_frame_pct"] = frameTime.GpuBoundPct;
            fields["frametime.avg_cpu_frame_ms"] = frameTime.P50FrameTimeMs; // approximation
            fields["frametime.avg_gpu_frame_ms"] = frameTime.P50FrameTimeMs;
            fields["frametime.stutter_correlates_with_pagefault"] = correlation?.CauseBreakdown.Any(c => c.Cause == "memory_pressure") ?? false;
        }
        else
        {
            fields["frametime.has_data"] = false;
        }

        // CPU fields
        if (metrics is not null)
        {
            fields["cpu.avg_load"] = metrics.Cpu.TotalLoad.Mean;
            fields["cpu.p95_load"] = metrics.Cpu.TotalLoad.P95;
            fields["cpu.dpc_time"] = metrics.Cpu.DpcTime.Mean;
            fields["cpu.single_core_sat_pct"] = metrics.Cpu.SingleCoreSaturationPct;
            fields["cpu.temp"] = metrics.Tier2.CpuTemp?.Mean;
            fields["cpu.clock"] = metrics.Tier2.CpuClock?.Mean;
            fields["cpu.score"] = (double?)(scores?.Cpu.Score);
        }

        // Memory fields
        if (metrics is not null)
        {
            fields["memory.avg_utilization"] = metrics.Memory.Utilization.Mean;
            fields["memory.hard_faults"] = metrics.Memory.HardFaults.Mean;
            fields["memory.committed_pct"] = metrics.Memory.CommitRatio.Mean;
            fields["memory.score"] = (double?)(scores?.Memory.Score);
            fields["memory.total_gb"] = hardware is not null ? (double)hardware.TotalRamGb : (double?)null;
            fields["memory.slots_available"] = hardware is not null ? (double)hardware.AvailableMemorySlots : (double?)null;
            fields["memory.installed_sticks"] = hardware is not null ? (double)hardware.RamSticks.Count : (double?)null;
            fields["memory.stick_capacity_gb"] = hardware?.RamSticks.Count > 0
                ? (double)(hardware.RamSticks[0].CapacityBytes / (1024 * 1024 * 1024))
                : (double?)null;
            fields["memory.speed_mhz"] = hardware?.RamSticks.Count > 0 ? (double)hardware.RamSticks[0].SpeedMhz : (double?)null;
            fields["memory.memory_type"] = hardware?.RamSticks.Count > 0 ? hardware.RamSticks[0].MemoryType : null;
        }

        // GPU fields
        if (metrics?.Gpu is not null)
        {
            fields["gpu.avg_load"] = metrics.Gpu.Load.Mean;
            fields["gpu.vram_utilization"] = metrics.Gpu.VramUtilization?.Mean;
            fields["gpu.score"] = (double?)(scores?.Gpu?.Score);
            fields["gpu.model"] = hardware?.GpuModel;
            fields["gpu.driver_version"] = hardware?.GpuDriverVersion;
        }

        // Disk fields
        if (metrics is not null)
        {
            fields["disk.avg_queue_length"] = metrics.Disk.QueueLength.Mean;
            fields["disk.avg_latency"] = (metrics.Disk.ReadLatency.Mean + metrics.Disk.WriteLatency.Mean) / 2;
            fields["disk.active_time"] = metrics.Disk.ActiveTime.Mean;
            fields["disk.score"] = (double?)(scores?.Disk.Score);
            fields["disk.os_drive_is_hdd"] = hardware?.Disks.Count > 0 && hardware.Disks[0].DriveType == "HDD";
            fields["disk.os_drive_model"] = hardware?.Disks.Count > 0 ? hardware.Disks[0].Model : null;
        }

        // Network fields
        if (metrics is not null)
        {
            fields["network.avg_utilization"] = metrics.Network.Utilization.Mean;
            fields["network.retransmit_rate"] = metrics.Network.Retransmits.Mean;
            fields["network.score"] = (double?)(scores?.Network.Score);
        }

        // System config fields
        if (sysConfig is not null)
        {
            fields["system.power_plan"] = sysConfig.PowerPlan;
            fields["system.game_mode"] = sysConfig.GameModeEnabled;
            fields["system.hags"] = sysConfig.HagsEnabled;
            fields["system.game_dvr"] = sysConfig.GameDvrEnabled;
            fields["system.sysmain_running"] = sysConfig.SysMainRunning;
            fields["system.wsearch_running"] = sysConfig.WSearchRunning;
        }

        // Culprit fields
        if (culprits is not null && culprits.HasAttribution)
        {
            fields["culprit.has_data"] = true;
            fields["culprit.top_process"] = culprits.TopContextSwitchProcesses.Count > 0
                ? culprits.TopContextSwitchProcesses[0].ProcessName : null;
            fields["culprit.interference_correlation"] = (double)culprits.InterferenceCorrelation;
        }
        else
        {
            fields["culprit.has_data"] = false;
        }

        return fields;
    }

    private static string ResolveConfidence(RecommendationConfig rec, ExpressionNode ast, Dictionary<string, object?> fields)
    {
        if (rec.Confidence != "auto")
            return rec.Confidence;

        // Auto-escalation: start low → medium if runtime data → high if evidence_boost matches
        string confidence = "low";

        // Check if trigger references runtime data
        var fieldRefs = ExpressionParser.GetFieldReferences(ast);
        bool referencesRuntime = fieldRefs.Any(f =>
            f.StartsWith("frametime.", StringComparison.Ordinal) ||
            f.StartsWith("culprit.", StringComparison.Ordinal) ||
            f.Contains("score", StringComparison.OrdinalIgnoreCase));

        if (referencesRuntime)
            confidence = "medium";

        // Check evidence_boost
        if (!string.IsNullOrEmpty(rec.EvidenceBoost))
        {
            try
            {
                var boostAst = ExpressionParser.Parse(rec.EvidenceBoost);
                var eval = new ExpressionEvaluator(fields);
                if (eval.Evaluate(boostAst))
                    confidence = "high";
            }
            catch
            {
                // Ignore malformed boost expressions
            }
        }

        return confidence;
    }

    private static string ResolveBody(string template, Dictionary<string, object?> fields)
    {
        string resolved = TemplateResolver.Resolve(template, fields);

        // Replace [unknown] with data-unavailable message for culprit fields
        if (resolved.Contains("[unknown]"))
        {
            resolved = resolved.Replace("[unknown]", DataUnavailableMessage);
        }

        return resolved;
    }

    private static List<string> CollectEvidence(ExpressionNode ast, Dictionary<string, object?> fields)
    {
        var evidence = new List<string>();
        var fieldRefs = ExpressionParser.GetFieldReferences(ast);

        foreach (var fieldRef in fieldRefs)
        {
            if (fields.TryGetValue(fieldRef, out var value) && value is not null)
            {
                string display = value switch
                {
                    double d => $"{fieldRef} = {d:F1}",
                    bool b => $"{fieldRef} = {(b ? "true" : "false")}",
                    string s => $"{fieldRef} = \"{s}\"",
                    _ => $"{fieldRef} = {value}"
                };
                evidence.Add(display);
            }
        }

        return evidence;
    }

    private static int SeverityOrder(string severity) => severity switch
    {
        "critical" => 3,
        "warning" => 2,
        "info" => 1,
        _ => 0
    };
}
