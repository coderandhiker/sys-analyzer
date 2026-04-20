using System.Text.Json;
using SysAnalyzer.Analysis.Models;
using SysAnalyzer.Capture;

namespace SysAnalyzer.Report;

/// <summary>
/// Generates a self-contained HTML report by injecting CSS, JS, and data into the report template.
/// All assets are inlined — no external URLs or CDN references.
/// </summary>
public class HtmlReportGenerator
{
    /// <summary>
    /// Generates a self-contained HTML report file.
    /// </summary>
    /// <param name="summary">The analysis summary data.</param>
    /// <param name="snapshots">Raw sensor snapshots for chart data (may be empty).</param>
    /// <param name="frameTimeSamples">Raw frame-time samples (may be null or empty).</param>
    /// <param name="outputDir">Directory to write the HTML file.</param>
    /// <param name="filenameFormat">Filename format string.</param>
    /// <param name="timestampFormat">Timestamp format for filename.</param>
    /// <param name="label">Optional capture label.</param>
    /// <returns>Path to the generated HTML file.</returns>
    public async Task<string> GenerateAsync(
        AnalysisSummary summary,
        IReadOnlyList<SensorSnapshot> snapshots,
        IReadOnlyList<FrameTimeSample>? frameTimeSamples,
        string outputDir,
        string filenameFormat,
        string timestampFormat,
        string? label = null)
    {
        Directory.CreateDirectory(outputDir);

        var filename = FilenameGenerator.Generate(filenameFormat, timestampFormat, label);
        var path = Path.Combine(outputDir, filename + ".html");

        var html = BuildHtml(summary, snapshots, frameTimeSamples);
        await File.WriteAllTextAsync(path, html);
        return path;
    }

    /// <summary>
    /// Builds the complete self-contained HTML string.
    /// </summary>
    public string BuildHtml(
        AnalysisSummary summary,
        IReadOnlyList<SensorSnapshot> snapshots,
        IReadOnlyList<FrameTimeSample>? frameTimeSamples)
    {
        var template = AssetLoader.Load("report-template.html");

        // Load all assets
        var tablerCss = AssetLoader.Load("tabler.min.css");
        var tablerJs = AssetLoader.Load("tabler.js");
        var tablerThemeJs = AssetLoader.Load("tabler-theme.js");
        var apexChartsJs = AssetLoader.Load("apexcharts.min.js");
        var reportCss = AssetLoader.Load("report.css");

        // Inject CSS/JS into placeholders
        template = template.Replace("/* TABLER_CSS_PLACEHOLDER */", tablerCss);
        template = template.Replace("/* REPORT_CSS_PLACEHOLDER */", reportCss);
        template = template.Replace("/* TABLER_THEME_JS_PLACEHOLDER */", tablerThemeJs);
        template = template.Replace("/* APEXCHARTS_JS_PLACEHOLDER */", apexChartsJs);
        template = template.Replace("/* TABLER_JS_PLACEHOLDER */", tablerJs);

        // Build report data object
        var reportData = BuildReportData(summary, snapshots, frameTimeSamples);
        var jsonData = JsonSerializer.Serialize(reportData, JsonReportGenerator.Options);

        // Inject data
        template = template.Replace("/*REPORT_DATA_PLACEHOLDER*/{}", jsonData);

        return template;
    }

    private static Dictionary<string, object?> BuildReportData(
        AnalysisSummary summary,
        IReadOnlyList<SensorSnapshot> snapshots,
        IReadOnlyList<FrameTimeSample>? frameTimeSamples)
    {
        var data = new Dictionary<string, object?>
        {
            ["metadata"] = summary.Metadata,
            ["fingerprint"] = summary.Fingerprint,
            ["sensorHealth"] = summary.SensorHealth,
            ["hardwareInventory"] = summary.HardwareInventory,
            ["systemConfiguration"] = summary.SystemConfiguration,
            ["scores"] = summary.Scores,
            ["frameTime"] = summary.FrameTime,
            ["culpritAttribution"] = summary.CulpritAttribution,
            ["recommendations"] = summary.Recommendations,
            ["baselineComparison"] = summary.BaselineComparison,
            ["selfOverhead"] = summary.SelfOverhead,
            ["timeSeries"] = summary.TimeSeries,
            ["topMemoryProcesses"] = summary.TopMemoryProcesses
        };

        // Prepare chart data
        var chartData = ChartDataPreparer.PrepareAll(
            snapshots,
            summary.FrameTime,
            frameTimeSamples,
            summary.Metadata.DurationSeconds);

        data["charts"] = chartData;

        return data;
    }
}
