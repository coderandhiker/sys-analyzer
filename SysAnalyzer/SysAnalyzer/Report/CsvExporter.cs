using System.Globalization;
using System.Text;
using SysAnalyzer.Capture;

namespace SysAnalyzer.Report;

/// <summary>
/// Exports time-series and PresentMon frame data as CSV files.
/// Only generated when the --csv flag is passed.
/// </summary>
public static class CsvExporter
{
    /// <summary>
    /// Exports 1-second granularity time-series CSV from sensor snapshots.
    /// </summary>
    public static async Task<string> ExportTimeSeriesAsync(
        IReadOnlyList<SensorSnapshot> snapshots,
        string outputDir,
        string filenameBase)
    {
        Directory.CreateDirectory(outputDir);
        var path = Path.Combine(outputDir, filenameBase + "-timeseries.csv");

        var sb = new StringBuilder();

        // Header
        sb.AppendLine(string.Join(",",
            "timestamp_s",
            "cpu_total_pct",
            "context_switches_per_sec",
            "dpc_time_pct",
            "interrupts_per_sec",
            "memory_util_pct",
            "available_memory_mb",
            "page_faults_per_sec",
            "hard_faults_per_sec",
            "committed_bytes",
            "committed_bytes_in_use_pct",
            "gpu_util_pct",
            "gpu_memory_util_pct",
            "gpu_memory_used_mb",
            "disk_active_pct",
            "disk_queue_length",
            "disk_bytes_per_sec",
            "disk_read_latency_ms",
            "disk_write_latency_ms",
            "network_bytes_per_sec",
            "network_util_pct",
            "tcp_retransmits_per_sec",
            "cpu_temp_c",
            "cpu_clock_mhz",
            "cpu_power_w",
            "gpu_temp_c",
            "gpu_clock_mhz",
            "gpu_power_w",
            "gpu_fan_rpm"
        ));

        // Rows — one per snapshot at capture interval (already ~1s)
        foreach (var s in snapshots)
        {
            sb.AppendLine(string.Join(",",
                s.Timestamp.ToSeconds().ToString("F3", CultureInfo.InvariantCulture),
                Fmt(s.TotalCpuPercent),
                Fmt(s.ContextSwitchesPerSec),
                Fmt(s.DpcTimePercent),
                Fmt(s.InterruptsPerSec),
                Fmt(s.MemoryUtilizationPercent),
                Fmt(s.AvailableMemoryMb),
                Fmt(s.PageFaultsPerSec),
                Fmt(s.HardFaultsPerSec),
                Fmt(s.CommittedBytes),
                Fmt(s.CommittedBytesInUsePercent),
                FmtN(s.GpuUtilizationPercent),
                FmtN(s.GpuMemoryUtilizationPercent),
                FmtN(s.GpuMemoryUsedMb),
                Fmt(s.DiskActiveTimePercent),
                Fmt(s.DiskQueueLength),
                Fmt(s.DiskBytesPerSec),
                Fmt(s.DiskReadLatencyMs),
                Fmt(s.DiskWriteLatencyMs),
                Fmt(s.NetworkBytesPerSec),
                Fmt(s.NetworkUtilizationPercent),
                Fmt(s.TcpRetransmitsPerSec),
                FmtN(s.CpuTempC),
                FmtN(s.CpuClockMhz),
                FmtN(s.CpuPowerW),
                FmtN(s.GpuTempC),
                FmtN(s.GpuClockMhz),
                FmtN(s.GpuPowerW),
                FmtN(s.GpuFanRpm)
            ));
        }

        await File.WriteAllTextAsync(path, sb.ToString());
        return path;
    }

    /// <summary>
    /// Exports raw per-frame PresentMon data as CSV.
    /// </summary>
    public static async Task<string> ExportPresentMonAsync(
        IReadOnlyList<FrameTimeSample> samples,
        string outputDir,
        string filenameBase)
    {
        Directory.CreateDirectory(outputDir);
        var path = Path.Combine(outputDir, filenameBase + "-presentmon.csv");

        var sb = new StringBuilder();

        // Header
        sb.AppendLine(string.Join(",",
            "timestamp_s",
            "app",
            "frame_time_ms",
            "cpu_busy_ms",
            "gpu_busy_ms",
            "dropped",
            "present_mode"
        ));

        foreach (var s in samples)
        {
            sb.AppendLine(string.Join(",",
                s.Timestamp.ToSeconds().ToString("F6", CultureInfo.InvariantCulture),
                CsvEscape(s.ApplicationName),
                s.FrameTimeMs.ToString("F3", CultureInfo.InvariantCulture),
                s.CpuBusyMs.ToString("F3", CultureInfo.InvariantCulture),
                s.GpuBusyMs.ToString("F3", CultureInfo.InvariantCulture),
                s.Dropped ? "1" : "0",
                CsvEscape(s.PresentMode)
            ));
        }

        await File.WriteAllTextAsync(path, sb.ToString());
        return path;
    }

    private static string Fmt(double value) =>
        value.ToString("G", CultureInfo.InvariantCulture);

    private static string FmtN(double? value) =>
        value.HasValue ? value.Value.ToString("G", CultureInfo.InvariantCulture) : "";

    private static string CsvEscape(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}
