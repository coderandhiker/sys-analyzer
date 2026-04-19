using SysAnalyzer.Analysis.Models;

namespace SysAnalyzer.Analysis;

/// <summary>
/// Maps CulpritAttributionResult to AnalysisSummary-compatible CulpritAttribution
/// and populates the flat culprit.* namespace for recommendation triggers.
/// </summary>
public static class CulpritResultMapper
{
    /// <summary>
    /// Convert the full attribution result to the JSON-compatible CulpritAttribution record.
    /// </summary>
    public static CulpritAttribution? ToSummary(CulpritAttributionResult? result)
    {
        if (result == null || !result.HasAttribution)
            return null;

        var topProcesses = result.TopContextSwitchProcesses
            .Select(p => new ProcessEntry(
                p.ProcessName,
                Math.Round(p.PercentOfTotal, 1),
                p.Description,
                p.Remediation,
                Math.Round(p.CorrelationWithStutter, 3)))
            .ToList();

        var topDpcDrivers = result.TopDpcDrivers
            .Select(d => new DpcDriverEntry(
                d.DriverModule,
                Math.Round(d.PercentOfDpcTime, 1),
                d.Description))
            .ToList();

        var topDiskProcesses = result.TopDiskIoProcesses
            .Select(d => new DiskProcessEntry(
                d.ProcessName,
                Math.Round(d.PercentOfTotal, 1),
                d.Description,
                d.Remediation,
                Math.Round(d.CorrelationWithStutter, 3)))
            .ToList();

        var lifetimeEntries = result.ProcessLifetimeEvents
            .Select(e => new ProcessLifetimeInfo(
                e.ProcessName,
                e.ProcessId,
                e.IsStart,
                Math.Round(e.TimestampSeconds, 3),
                e.CorrelatesWithStutterCluster))
            .ToList();

        return new CulpritAttribution(
            TopProcesses: topProcesses,
            TopDpcDrivers: topDpcDrivers,
            TopDiskProcesses: topDiskProcesses,
            HasAttribution: result.HasAttribution,
            HasDpcAttribution: result.HasDpcAttribution,
            InterferenceCorrelation: result.InterferenceCorrelation,
            ProcessLifetimeEvents: lifetimeEntries.Count > 0 ? lifetimeEntries : null);
    }

    /// <summary>
    /// Populate the flat culprit.* dictionary namespace for recommendation trigger evaluation.
    /// </summary>
    public static void PopulateFlatDictionary(Dictionary<string, object> dict, CulpritAttributionResult? result)
    {
        if (result == null || !result.HasAttribution)
        {
            dict["culprit.has_attribution"] = false;
            dict["culprit.has_dpc_attribution"] = false;
            return;
        }

        dict["culprit.has_attribution"] = result.HasAttribution;
        dict["culprit.has_dpc_attribution"] = result.HasDpcAttribution;
        dict["culprit.interference_correlation"] = (double)result.InterferenceCorrelation;

        // Top context-switch process
        if (result.TopContextSwitchProcesses.Count > 0)
        {
            var top = result.TopContextSwitchProcesses[0];
            dict["culprit.top_process_name"] = top.ProcessName;
            dict["culprit.top_process_ctx_switch_pct"] = (double)top.PercentOfTotal;
            dict["culprit.top_process_description"] = top.Description ?? "";
            dict["culprit.top_process_remediation"] = top.Remediation ?? "";
            dict["culprit.process_summary"] = BuildProcessSummary(result.TopContextSwitchProcesses);
        }

        // Top DPC driver
        if (result.TopDpcDrivers.Count > 0)
        {
            var top = result.TopDpcDrivers[0];
            dict["culprit.top_dpc_driver"] = top.DriverModule;
            dict["culprit.top_dpc_driver_pct"] = (double)top.PercentOfDpcTime;
            dict["culprit.dpc_summary"] = BuildDpcSummary(result.TopDpcDrivers);
        }

        // Top disk I/O process
        if (result.TopDiskIoProcesses.Count > 0)
        {
            var top = result.TopDiskIoProcesses[0];
            dict["culprit.disk_io_top_process"] = top.ProcessName;
            dict["culprit.disk_io_summary"] = BuildDiskIoSummary(result.TopDiskIoProcesses);
        }
    }

    private static string BuildProcessSummary(IReadOnlyList<ProcessCulprit> processes)
    {
        if (processes.Count == 0) return "";
        var top3 = processes.Take(3)
            .Select(p => $"{p.ProcessName} ({p.PercentOfTotal:F1}%)")
            .ToArray();
        return string.Join(", ", top3);
    }

    private static string BuildDpcSummary(IReadOnlyList<DriverCulprit> drivers)
    {
        if (drivers.Count == 0) return "";
        var top3 = drivers.Take(3)
            .Select(d => $"{d.DriverModule} ({d.PercentOfDpcTime:F1}%)")
            .ToArray();
        return string.Join(", ", top3);
    }

    private static string BuildDiskIoSummary(IReadOnlyList<ProcessCulprit> processes)
    {
        if (processes.Count == 0) return "";
        var top3 = processes.Take(3)
            .Select(p => $"{p.ProcessName} ({p.PercentOfTotal:F1}%)")
            .ToArray();
        return string.Join(", ", top3);
    }
}
