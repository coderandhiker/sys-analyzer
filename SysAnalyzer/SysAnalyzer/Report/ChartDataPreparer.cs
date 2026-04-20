using System.Text.Json;
using SysAnalyzer.Analysis.Models;
using SysAnalyzer.Capture;

namespace SysAnalyzer.Report;

/// <summary>
/// Prepares chart data configurations for ApexCharts rendering in the HTML report.
/// Applies LTTB downsampling to keep chart data under 2000 points.
/// </summary>
public static class ChartDataPreparer
{
    /// <summary>
    /// Builds the complete chart data object for injection into the HTML template.
    /// </summary>
    public static Dictionary<string, object> PrepareAll(
        IReadOnlyList<SensorSnapshot> snapshots,
        FrameTimeSummary? frameTime,
        IReadOnlyList<FrameTimeSample>? frameTimeSamples,
        double durationSeconds)
    {
        var charts = new Dictionary<string, object>();

        if (snapshots.Count > 0)
        {
            int target = LttbDownsampler.GetTargetPointCount(durationSeconds, snapshots.Count);

            charts["cpuUtilization"] = PrepareCpuChart(snapshots, target);
            charts["memoryUtilization"] = PrepareMemoryChart(snapshots, target);
            charts["diskActivity"] = PrepareDiskChart(snapshots, target);
            charts["networkThroughput"] = PrepareNetworkChart(snapshots, target);

            if (snapshots.Any(s => s.GpuUtilizationPercent.HasValue))
                charts["gpuLoad"] = PrepareGpuChart(snapshots, target);
        }

        if (frameTimeSamples != null && frameTimeSamples.Count > 0)
        {
            int frameTarget = LttbDownsampler.GetTargetPointCount(durationSeconds, frameTimeSamples.Count);
            // Frame data can be very dense (60-144Hz), always downsample to max 2000
            frameTarget = Math.Min(frameTarget, 2000);

            charts["frameTimeOverTime"] = PrepareFrameTimeChart(frameTimeSamples, frameTarget);
            charts["frameTimeDistribution"] = PrepareFrameTimeHistogram(frameTimeSamples);

            if (frameTime != null)
                charts["fpsOverTime"] = PrepareFpsChart(frameTimeSamples, frameTarget);
        }

        return charts;
    }

    private static object PrepareCpuChart(IReadOnlyList<SensorSnapshot> snapshots, int target)
    {
        var times = snapshots.Select(s => s.Timestamp.ToSeconds()).ToArray();
        var total = snapshots.Select(s => s.TotalCpuPercent).ToArray();

        var downsampled = LttbDownsampler.Downsample(times, total, target, spikeThresholdMultiplier: 10.0);

        var series = new List<object>
        {
            new { name = "Total CPU %", data = downsampled.Select(p => new { x = p.X, y = Math.Round(p.Y, 1) }).ToArray() }
        };

        // Add per-core data if available (downsample each core separately)
        int coreCount = snapshots[0].PerCoreCpuPercent?.Length ?? 0;
        if (coreCount > 0 && coreCount <= 16) // only show per-core for <=16 cores to avoid clutter
        {
            for (int c = 0; c < coreCount; c++)
            {
                int core = c;
                var coreValues = snapshots.Select(s => s.PerCoreCpuPercent[core]).ToArray();
                var coreDown = LttbDownsampler.Downsample(times, coreValues, target, spikeThresholdMultiplier: 10.0);
                series.Add(new { name = $"Core {core}", data = coreDown.Select(p => new { x = p.X, y = Math.Round(p.Y, 1) }).ToArray() });
            }
        }

        return new { type = "area", series, threshold = 95.0 };
    }

    private static object PrepareMemoryChart(IReadOnlyList<SensorSnapshot> snapshots, int target)
    {
        var times = snapshots.Select(s => s.Timestamp.ToSeconds()).ToArray();
        var memPct = snapshots.Select(s => s.MemoryUtilizationPercent).ToArray();
        var pageFaults = snapshots.Select(s => s.PageFaultsPerSec).ToArray();

        var memDown = LttbDownsampler.Downsample(times, memPct, target, spikeThresholdMultiplier: 10.0);
        var pfDown = LttbDownsampler.Downsample(times, pageFaults, target, spikeThresholdMultiplier: 5.0);

        return new
        {
            type = "line",
            series = new object[]
            {
                new { name = "Memory %", data = memDown.Select(p => new { x = p.X, y = Math.Round(p.Y, 1) }).ToArray(), yAxisIndex = 0 },
                new { name = "Page Faults/s", data = pfDown.Select(p => new { x = p.X, y = Math.Round(p.Y, 0) }).ToArray(), yAxisIndex = 1 }
            },
            dualAxis = true
        };
    }

    private static object PrepareGpuChart(IReadOnlyList<SensorSnapshot> snapshots, int target)
    {
        var times = snapshots.Select(s => s.Timestamp.ToSeconds()).ToArray();
        var gpuPct = snapshots.Select(s => s.GpuUtilizationPercent ?? 0).ToArray();
        var vramPct = snapshots.Select(s => s.GpuMemoryUtilizationPercent ?? 0).ToArray();

        var gpuDown = LttbDownsampler.Downsample(times, gpuPct, target, spikeThresholdMultiplier: 10.0);
        var vramDown = LttbDownsampler.Downsample(times, vramPct, target, spikeThresholdMultiplier: 10.0);

        var series = new List<object>
        {
            new { name = "GPU %", data = gpuDown.Select(p => new { x = p.X, y = Math.Round(p.Y, 1) }).ToArray(), yAxisIndex = 0 },
            new { name = "VRAM %", data = vramDown.Select(p => new { x = p.X, y = Math.Round(p.Y, 1) }).ToArray(), yAxisIndex = 0 }
        };

        // Add temperature on secondary axis if Tier 2 data exists
        bool hasTempData = snapshots.Any(s => s.GpuTempC.HasValue);
        if (hasTempData)
        {
            var temps = snapshots.Select(s => s.GpuTempC ?? 0).ToArray();
            var tempDown = LttbDownsampler.Downsample(times, temps, target, spikeThresholdMultiplier: 10.0);
            series.Add(new { name = "GPU Temp °C", data = tempDown.Select(p => new { x = p.X, y = Math.Round(p.Y, 1) }).ToArray(), yAxisIndex = 1 });
        }

        return new
        {
            type = "line",
            series,
            dualAxis = hasTempData
        };
    }

    private static object PrepareDiskChart(IReadOnlyList<SensorSnapshot> snapshots, int target)
    {
        var times = snapshots.Select(s => s.Timestamp.ToSeconds()).ToArray();
        var active = snapshots.Select(s => s.DiskActiveTimePercent).ToArray();
        var queue = snapshots.Select(s => s.DiskQueueLength).ToArray();

        var activeDown = LttbDownsampler.Downsample(times, active, target, spikeThresholdMultiplier: 5.0);
        var queueDown = LttbDownsampler.Downsample(times, queue, target, spikeThresholdMultiplier: 5.0);

        return new
        {
            type = "line",
            series = new object[]
            {
                new { name = "Disk Active %", data = activeDown.Select(p => new { x = p.X, y = Math.Round(p.Y, 1) }).ToArray() },
                new { name = "Queue Length", data = queueDown.Select(p => new { x = p.X, y = Math.Round(p.Y, 2) }).ToArray() }
            }
        };
    }

    private static object PrepareNetworkChart(IReadOnlyList<SensorSnapshot> snapshots, int target)
    {
        var times = snapshots.Select(s => s.Timestamp.ToSeconds()).ToArray();
        var bytes = snapshots.Select(s => s.NetworkBytesPerSec).ToArray();
        var utilPct = snapshots.Select(s => s.NetworkUtilizationPercent).ToArray();

        var bytesDown = LttbDownsampler.Downsample(times, bytes, target, spikeThresholdMultiplier: 5.0);
        var utilDown = LttbDownsampler.Downsample(times, utilPct, target, spikeThresholdMultiplier: 5.0);

        return new
        {
            type = "area",
            series = new object[]
            {
                new { name = "Bytes/sec", data = bytesDown.Select(p => new { x = p.X, y = Math.Round(p.Y, 0) }).ToArray() },
                new { name = "Utilization %", data = utilDown.Select(p => new { x = p.X, y = Math.Round(p.Y, 1) }).ToArray() }
            }
        };
    }

    private static object PrepareFrameTimeChart(IReadOnlyList<FrameTimeSample> samples, int target)
    {
        var data = samples.Select(s => (X: s.Timestamp.ToSeconds(), Y: s.FrameTimeMs)).ToArray();
        var downsampled = LttbDownsampler.Downsample(data.AsSpan(), target);

        return new
        {
            type = "line",
            series = new object[]
            {
                new { name = "Frame Time (ms)", data = downsampled.Select(p => new { x = p.X, y = Math.Round(p.Y, 2) }).ToArray() }
            }
        };
    }

    private static object PrepareFrameTimeHistogram(IReadOnlyList<FrameTimeSample> samples)
    {
        var buckets = new (string Label, double Min, double Max)[]
        {
            ("0-8ms", 0, 8),
            ("8-16ms", 8, 16),
            ("16-33ms", 16, 33),
            ("33-50ms", 33, 50),
            ("50-100ms", 50, 100),
            ("100ms+", 100, double.MaxValue)
        };

        var counts = new int[buckets.Length];
        foreach (var s in samples)
        {
            for (int i = 0; i < buckets.Length; i++)
            {
                if (s.FrameTimeMs >= buckets[i].Min && s.FrameTimeMs < buckets[i].Max)
                {
                    counts[i]++;
                    break;
                }
            }
        }

        return new
        {
            type = "bar",
            categories = buckets.Select(b => b.Label).ToArray(),
            series = new object[]
            {
                new { name = "Frames", data = counts }
            },
            logarithmic = true
        };
    }

    private static object PrepareFpsChart(IReadOnlyList<FrameTimeSample> samples, int target)
    {
        var data = samples
            .Where(s => s.FrameTimeMs > 0)
            .Select(s => (X: s.Timestamp.ToSeconds(), Y: 1000.0 / s.FrameTimeMs))
            .ToArray();

        var downsampled = LttbDownsampler.Downsample(data.AsSpan(), target);

        return new
        {
            type = "line",
            series = new object[]
            {
                new { name = "FPS", data = downsampled.Select(p => new { x = p.X, y = Math.Round(p.Y, 1) }).ToArray() }
            }
        };
    }
}
