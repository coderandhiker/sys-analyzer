using SysAnalyzer.Capture;

namespace SysAnalyzer.Capture.Providers;

/// <summary>
/// Produces a metric reading each polling tick. Called synchronously on the capture loop.
/// Contract: Poll() must complete in &lt;10ms. Failed polls return MetricBatch.Empty.
/// Used by: PerformanceCounterProvider, LibreHardwareProvider.
/// </summary>
public interface IPolledProvider : IProvider
{
    MetricBatch Poll(long qpcTimestamp);
}
