using System.Diagnostics;

namespace SysAnalyzer.Capture.Providers;

public sealed class SystemPerfCounterFactory : IPerfCounterFactory
{
    public IPerfCounterHandle? TryCreate(string category, string counter, string instance)
    {
        try
        {
            var pc = string.IsNullOrEmpty(instance)
                ? new PerformanceCounter(category, counter)
                : new PerformanceCounter(category, counter, instance);
            // Warm up — first read is always 0 for rate counters
            pc.NextValue();
            return new SystemPerfCounterHandle(pc);
        }
        catch
        {
            return null;
        }
    }
}

internal sealed class SystemPerfCounterHandle : IPerfCounterHandle
{
    private readonly PerformanceCounter _counter;
    public SystemPerfCounterHandle(PerformanceCounter counter) => _counter = counter;
    public float NextValue() => _counter.NextValue();
    public void Dispose() => _counter.Dispose();
}
