using SysAnalyzer.Analysis.Models;
using SysAnalyzer.Capture;
using SysAnalyzer.Data;

namespace SysAnalyzer.Analysis;

/// <summary>
/// Correlates ETW events with frame-time stutter spikes to identify culprit processes and drivers.
/// </summary>
public class CulpritAttributor
{
    private const int MaxResults = 10;
    private static readonly TimeSpan StutterClusterWindow = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Compute culprit attribution by correlating ETW events with stutter spike timestamps.
    /// </summary>
    /// <param name="stutterSpikes">Timestamps of frame-time stutter spikes from PresentMon data.</param>
    /// <param name="etwEvents">All collected ETW events from the capture session.</param>
    /// <param name="hasEtw">Whether ETW data was available at all.</param>
    /// <param name="hasDpc">Whether DPC events were captured.</param>
    /// <param name="eventsLost">Number of ETW events lost (buffer overflow).</param>
    public CulpritAttributionResult Attribute(
        IReadOnlyList<QpcTimestamp> stutterSpikes,
        IReadOnlyList<EtwEvent> etwEvents,
        bool hasEtw,
        bool hasDpc,
        int eventsLost)
    {
        if (!hasEtw || etwEvents.Count == 0)
        {
            return new CulpritAttributionResult(
                TopContextSwitchProcesses: [],
                TopDpcDrivers: [],
                TopDiskIoProcesses: [],
                ProcessLifetimeEvents: [],
                HasAttribution: false,
                HasDpcAttribution: false,
                InterferenceCorrelation: 0f);
        }

        // Separate events by type
        var contextSwitches = new List<ContextSwitchEvent>();
        var dpcEvents = new List<DpcEvent>();
        var diskIoEvents = new List<DiskIoEvent>();
        var lifetimeEvents = new List<ProcessLifetimeEvent>();

        foreach (var evt in etwEvents)
        {
            switch (evt)
            {
                case ContextSwitchEvent cs: contextSwitches.Add(cs); break;
                case DpcEvent dpc: dpcEvents.Add(dpc); break;
                case DiskIoEvent dio: diskIoEvents.Add(dio); break;
                case ProcessLifetimeEvent ple: lifetimeEvents.Add(ple); break;
            }
        }

        // Correlate context switches with stutter spikes
        var topProcesses = CorrelateContextSwitches(stutterSpikes, contextSwitches);

        // Correlate DPC events with stutter spikes
        var topDrivers = CorrelateDpcEvents(stutterSpikes, dpcEvents);

        // Correlate disk I/O events with stutter spikes
        var topDiskProcesses = CorrelateDiskIo(stutterSpikes, diskIoEvents);

        // Process lifetime tracking
        var lifetimeEntries = TrackProcessLifetimes(lifetimeEvents, stutterSpikes);

        // Compute overall interference correlation
        float overallCorrelation = ComputeOverallCorrelation(topProcesses, topDrivers, topDiskProcesses);

        return new CulpritAttributionResult(
            TopContextSwitchProcesses: topProcesses,
            TopDpcDrivers: topDrivers,
            TopDiskIoProcesses: topDiskProcesses,
            ProcessLifetimeEvents: lifetimeEntries,
            HasAttribution: true,
            HasDpcAttribution: hasDpc && dpcEvents.Count > 0,
            InterferenceCorrelation: overallCorrelation);
    }

    internal static List<ProcessCulprit> CorrelateContextSwitches(
        IReadOnlyList<QpcTimestamp> spikes,
        IReadOnlyList<ContextSwitchEvent> events)
    {
        if (events.Count == 0 || spikes.Count == 0)
            return [];

        // For each spike, find context switches within ±50ms
        var processCounts = new Dictionary<string, (int Count, int Pid, HashSet<int> SpikeIndices)>();
        int totalSwitches = 0;

        for (int i = 0; i < spikes.Count; i++)
        {
            var spike = spikes[i];
            foreach (var cs in events)
            {
                if (TimeWindow.IsWithin(cs.Timestamp, spike, CorrelationWindows.FrameSpikeNarrow))
                {
                    string key = cs.NewProcessName;
                    if (!processCounts.TryGetValue(key, out var entry))
                    {
                        entry = (0, cs.NewProcessId, new HashSet<int>());
                        processCounts[key] = entry;
                    }

                    entry.SpikeIndices.Add(i);
                    processCounts[key] = (entry.Count + 1, entry.Pid, entry.SpikeIndices);
                    totalSwitches++;
                }
            }
        }

        if (totalSwitches == 0)
            return [];

        return processCounts
            .OrderByDescending(kv => kv.Value.Count)
            .Take(MaxResults)
            .Select(kv =>
            {
                float pctOfTotal = (float)kv.Value.Count / totalSwitches * 100f;
                float correlation = (float)kv.Value.SpikeIndices.Count / spikes.Count;
                KnownProcesses.TryGetInfo(kv.Key, out string desc, out string remediation);

                return new ProcessCulprit(
                    ProcessName: kv.Key,
                    ProcessId: kv.Value.Pid,
                    ContextSwitchCount: kv.Value.Count,
                    PercentOfTotal: pctOfTotal,
                    CorrelationWithStutter: correlation,
                    Description: string.IsNullOrEmpty(desc) ? null : desc,
                    Remediation: string.IsNullOrEmpty(remediation) ? null : remediation);
            })
            .ToList();
    }

    internal static List<DriverCulprit> CorrelateDpcEvents(
        IReadOnlyList<QpcTimestamp> spikes,
        IReadOnlyList<DpcEvent> events)
    {
        if (events.Count == 0 || spikes.Count == 0)
            return [];

        // For each spike, find DPC events within ±500ms
        var driverTotals = new Dictionary<string, double>();
        double totalDpcTime = 0;

        foreach (var spike in spikes)
        {
            foreach (var dpc in events)
            {
                if (TimeWindow.IsWithin(dpc.Timestamp, spike, CorrelationWindows.FrameSpikeWide))
                {
                    double timeMs = dpc.DurationUs / 1000.0;
                    if (!driverTotals.TryGetValue(dpc.DriverModule, out double current))
                        current = 0;
                    driverTotals[dpc.DriverModule] = current + timeMs;
                    totalDpcTime += timeMs;
                }
            }
        }

        if (totalDpcTime <= 0)
            return [];

        return driverTotals
            .OrderByDescending(kv => kv.Value)
            .Take(MaxResults)
            .Select(kv => new DriverCulprit(
                DriverModule: kv.Key,
                TotalDpcTimeMs: kv.Value,
                PercentOfDpcTime: (float)(kv.Value / totalDpcTime * 100.0),
                Description: KnownProcesses.GetDescription(kv.Key)))
            .ToList();
    }

    internal static List<ProcessCulprit> CorrelateDiskIo(
        IReadOnlyList<QpcTimestamp> spikes,
        IReadOnlyList<DiskIoEvent> events)
    {
        if (events.Count == 0 || spikes.Count == 0)
            return [];

        // For each spike, find disk I/O within ±2s
        var processBytes = new Dictionary<string, (long Bytes, int Pid, HashSet<int> SpikeIndices)>();
        long totalBytes = 0;

        for (int i = 0; i < spikes.Count; i++)
        {
            var spike = spikes[i];
            foreach (var dio in events)
            {
                if (TimeWindow.IsWithin(dio.Timestamp, spike, CorrelationWindows.MetricCorrelation))
                {
                    string key = dio.ProcessName;
                    if (!processBytes.TryGetValue(key, out var entry))
                    {
                        entry = (0L, dio.ProcessId, new HashSet<int>());
                        processBytes[key] = entry;
                    }

                    entry.SpikeIndices.Add(i);
                    processBytes[key] = (entry.Bytes + dio.BytesTransferred, entry.Pid, entry.SpikeIndices);
                    totalBytes += dio.BytesTransferred;
                }
            }
        }

        if (totalBytes == 0)
            return [];

        return processBytes
            .OrderByDescending(kv => kv.Value.Bytes)
            .Take(MaxResults)
            .Select(kv =>
            {
                float pctOfTotal = (float)((double)kv.Value.Bytes / totalBytes * 100.0);
                float correlation = (float)kv.Value.SpikeIndices.Count / spikes.Count;
                KnownProcesses.TryGetInfo(kv.Key, out string desc, out string remediation);

                return new ProcessCulprit(
                    ProcessName: kv.Key,
                    ProcessId: kv.Value.Pid,
                    ContextSwitchCount: 0, // Not applicable for disk I/O — reuse field as byte count would break type
                    PercentOfTotal: pctOfTotal,
                    CorrelationWithStutter: correlation,
                    Description: string.IsNullOrEmpty(desc) ? null : desc,
                    Remediation: string.IsNullOrEmpty(remediation) ? null : remediation);
            })
            .ToList();
    }

    internal static List<ProcessLifetimeEntry> TrackProcessLifetimes(
        IReadOnlyList<ProcessLifetimeEvent> lifetimeEvents,
        IReadOnlyList<QpcTimestamp> stutterSpikes)
    {
        var entries = new List<ProcessLifetimeEntry>();

        foreach (var evt in lifetimeEvents)
        {
            bool correlatesWithCluster = DoesCorrelateWithStutterCluster(evt.Timestamp, stutterSpikes);

            entries.Add(new ProcessLifetimeEntry(
                ProcessName: evt.ProcessName,
                ProcessId: evt.ProcessId,
                IsStart: evt.IsStart,
                TimestampSeconds: evt.Timestamp.ToSeconds(),
                CorrelatesWithStutterCluster: correlatesWithCluster));
        }

        return entries;
    }

    internal static bool DoesCorrelateWithStutterCluster(
        QpcTimestamp eventTime,
        IReadOnlyList<QpcTimestamp> stutterSpikes)
    {
        if (stutterSpikes.Count < 2)
            return false;

        // A stutter cluster is 2+ spikes within 10 seconds of each other
        // Check if event time is within 10 seconds of a cluster
        for (int i = 0; i < stutterSpikes.Count - 1; i++)
        {
            var spike = stutterSpikes[i];
            var nextSpike = stutterSpikes[i + 1];

            // Check if these two spikes form a cluster (within 10s of each other)
            double gapSeconds = Math.Abs((nextSpike.RawTicks - spike.RawTicks) / (double)QpcTimestamp.Frequency);
            if (gapSeconds <= StutterClusterWindow.TotalSeconds)
            {
                // Check if the event is within ±10s of this cluster
                if (TimeWindow.IsWithin(eventTime, spike, StutterClusterWindow) ||
                    TimeWindow.IsWithin(eventTime, nextSpike, StutterClusterWindow))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static float ComputeOverallCorrelation(
        IReadOnlyList<ProcessCulprit> contextSwitchProcesses,
        IReadOnlyList<DriverCulprit> dpcDrivers,
        IReadOnlyList<ProcessCulprit> diskIoProcesses)
    {
        // Overall interference correlation: max correlation across all culprit types
        float maxCorrelation = 0f;

        if (contextSwitchProcesses.Count > 0)
            maxCorrelation = Math.Max(maxCorrelation, contextSwitchProcesses[0].CorrelationWithStutter);

        if (diskIoProcesses.Count > 0)
            maxCorrelation = Math.Max(maxCorrelation, diskIoProcesses[0].CorrelationWithStutter);

        // DPC drivers don't have per-spike correlation, approximate from percentage
        if (dpcDrivers.Count > 0)
            maxCorrelation = Math.Max(maxCorrelation, dpcDrivers[0].PercentOfDpcTime / 100f);

        return maxCorrelation;
    }
}
