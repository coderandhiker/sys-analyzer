namespace SysAnalyzer.Capture.Providers;

public sealed class PerformanceCounterProvider : IPolledProvider
{
    public string Name => "PerformanceCounters";
    public ProviderTier RequiredTier => ProviderTier.Tier1;
    public ProviderHealth Health { get; private set; } = new(ProviderStatus.Active, null, 0, 0, 0);

    private readonly IPerfCounterFactory _factory;

    // Pre-allocated batch — reused every poll to avoid heap allocations
    private MetricBatch _batch;

    // Counter handles
    private IPerfCounterHandle? _cpuTotal;
    private IPerfCounterHandle? _contextSwitches;
    private IPerfCounterHandle? _dpcTime;
    private IPerfCounterHandle? _interrupts;
    private IPerfCounterHandle? _memCommittedPct;
    private IPerfCounterHandle? _memAvailableMb;
    private IPerfCounterHandle? _memPageFaults;
    private IPerfCounterHandle? _memPagesPerSec;
    private IPerfCounterHandle? _memCommittedBytes;
    private IPerfCounterHandle? _memPoolNonpaged;
    private IPerfCounterHandle? _memCacheBytes;
    private IPerfCounterHandle? _gpuEngine;
    private IPerfCounterHandle? _gpuMemory;
    private IPerfCounterHandle? _diskTime;
    private IPerfCounterHandle? _diskQueueLength;
    private IPerfCounterHandle? _diskBytesPerSec;
    private IPerfCounterHandle? _diskReadLatency;
    private IPerfCounterHandle? _diskWriteLatency;
    private IPerfCounterHandle? _networkBytes;
    private IPerfCounterHandle? _tcpRetransmits;
    private IPerfCounterHandle? _systemProcesses;
    private IPerfCounterHandle? _systemThreads;
    private IPerfCounterHandle? _handleCount;
    private IPerfCounterHandle? _systemUpTime;

    private int _expectedMetrics;
    private int _availableMetrics;
    private readonly List<string> _missingCounters = new();

    public PerformanceCounterProvider(IPerfCounterFactory? factory = null)
    {
        _factory = factory ?? new SystemPerfCounterFactory();
    }

    public Task<ProviderHealth> InitAsync()
    {
        var counters = new (string category, string counter, string instance, Action<IPerfCounterHandle> assign)[]
        {
            ("Processor", "% Processor Time", "_Total", h => _cpuTotal = h),
            ("System", "Context Switches/sec", "", h => _contextSwitches = h),
            ("Processor", "% DPC Time", "_Total", h => _dpcTime = h),
            ("Processor", "Interrupts/sec", "_Total", h => _interrupts = h),
            ("Memory", "% Committed Bytes In Use", "", h => _memCommittedPct = h),
            ("Memory", "Available MBytes", "", h => _memAvailableMb = h),
            ("Memory", "Page Faults/sec", "", h => _memPageFaults = h),
            ("Memory", "Pages/sec", "", h => _memPagesPerSec = h),
            ("Memory", "Committed Bytes", "", h => _memCommittedBytes = h),
            ("Memory", "Pool Nonpaged Bytes", "", h => _memPoolNonpaged = h),
            ("Memory", "Cache Bytes", "", h => _memCacheBytes = h),
            ("GPU Engine", "Utilization Percentage", "_Total", h => _gpuEngine = h),
            ("GPU Process Memory", "Dedicated Usage", "_Total", h => _gpuMemory = h),
            ("PhysicalDisk", "% Disk Time", "_Total", h => _diskTime = h),
            ("PhysicalDisk", "Avg. Disk Queue Length", "_Total", h => _diskQueueLength = h),
            ("PhysicalDisk", "Disk Bytes/sec", "_Total", h => _diskBytesPerSec = h),
            ("PhysicalDisk", "Avg. Disk sec/Read", "_Total", h => _diskReadLatency = h),
            ("PhysicalDisk", "Avg. Disk sec/Write", "_Total", h => _diskWriteLatency = h),
            ("Network Interface", "Bytes Total/sec", "*", h => _networkBytes = h),
            ("TCPv4", "Segments Retransmitted/sec", "", h => _tcpRetransmits = h),
            ("System", "Processes", "", h => _systemProcesses = h),
            ("System", "Threads", "", h => _systemThreads = h),
            ("Process", "Handle Count", "_Total", h => _handleCount = h),
            ("System", "System Up Time", "", h => _systemUpTime = h),
        };

        _expectedMetrics = counters.Length;

        foreach (var (category, counter, instance, assign) in counters)
        {
            var handle = _factory.TryCreate(category, counter, instance);
            if (handle != null)
            {
                assign(handle);
                _availableMetrics++;
            }
            else
            {
                _missingCounters.Add($@"{category}\{counter}");
            }
        }

        var status = _availableMetrics == 0
            ? ProviderStatus.Failed
            : _availableMetrics < _expectedMetrics
                ? ProviderStatus.Degraded
                : ProviderStatus.Active;

        var reason = _missingCounters.Count > 0
            ? $"Missing counters: {string.Join(", ", _missingCounters)}"
            : null;

        Health = new ProviderHealth(status, reason, _availableMetrics, _expectedMetrics, 0);
        return Task.FromResult(Health);
    }

    public MetricBatch Poll(long qpcTimestamp)
    {
        _batch = MetricBatch.Create();

        _batch.TotalCpuPercent = SafeRead(_cpuTotal);
        _batch.ContextSwitchesPerSec = SafeRead(_contextSwitches);
        _batch.DpcTimePercent = SafeRead(_dpcTime);
        _batch.InterruptsPerSec = SafeRead(_interrupts);

        _batch.CommittedBytesInUsePercent = SafeRead(_memCommittedPct);
        _batch.AvailableMemoryMb = SafeRead(_memAvailableMb);
        _batch.PageFaultsPerSec = SafeRead(_memPageFaults);
        _batch.HardFaultsPerSec = SafeRead(_memPagesPerSec);
        _batch.CommittedBytes = SafeRead(_memCommittedBytes);

        // Compute memory utilization: if available MB and committed % both present
        if (_memAvailableMb != null && _memCommittedPct != null)
        {
            _batch.MemoryUtilizationPercent = _batch.CommittedBytesInUsePercent;
        }

        // GPU counters — may not exist
        var gpuUtil = SafeReadNullable(_gpuEngine);
        var gpuMem = SafeReadNullable(_gpuMemory);
        _batch.GpuUtilizationPercent = gpuUtil ?? double.NaN;
        _batch.GpuMemoryUsedMb = gpuMem.HasValue ? gpuMem.Value / (1024.0 * 1024.0) : double.NaN;

        _batch.DiskActiveTimePercent = SafeRead(_diskTime);
        _batch.DiskQueueLength = SafeRead(_diskQueueLength);
        _batch.DiskBytesPerSec = SafeRead(_diskBytesPerSec);
        _batch.DiskReadLatencyMs = SafeRead(_diskReadLatency) * 1000.0; // sec to ms
        _batch.DiskWriteLatencyMs = SafeRead(_diskWriteLatency) * 1000.0;

        _batch.NetworkBytesPerSec = SafeRead(_networkBytes);
        _batch.TcpRetransmitsPerSec = SafeRead(_tcpRetransmits);

        return _batch;
    }

    private static double SafeRead(IPerfCounterHandle? handle)
    {
        if (handle == null) return 0;
        try { return handle.NextValue(); }
        catch { return 0; }
    }

    private static double? SafeReadNullable(IPerfCounterHandle? handle)
    {
        if (handle == null) return null;
        try { return handle.NextValue(); }
        catch { return null; }
    }

    public void Dispose()
    {
        _cpuTotal?.Dispose();
        _contextSwitches?.Dispose();
        _dpcTime?.Dispose();
        _interrupts?.Dispose();
        _memCommittedPct?.Dispose();
        _memAvailableMb?.Dispose();
        _memPageFaults?.Dispose();
        _memPagesPerSec?.Dispose();
        _memCommittedBytes?.Dispose();
        _memPoolNonpaged?.Dispose();
        _memCacheBytes?.Dispose();
        _gpuEngine?.Dispose();
        _gpuMemory?.Dispose();
        _diskTime?.Dispose();
        _diskQueueLength?.Dispose();
        _diskBytesPerSec?.Dispose();
        _diskReadLatency?.Dispose();
        _diskWriteLatency?.Dispose();
        _networkBytes?.Dispose();
        _tcpRetransmits?.Dispose();
        _systemProcesses?.Dispose();
        _systemThreads?.Dispose();
        _handleCount?.Dispose();
        _systemUpTime?.Dispose();
    }
}
