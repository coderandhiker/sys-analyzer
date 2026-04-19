using SysAnalyzer.Capture;

namespace SysAnalyzer.Capture.Providers;

/// <summary>
/// Produces asynchronous events with their own timestamps.
/// Used by: EtwProvider, PresentMonProvider.
/// Contract: Events carry native timestamps normalized to captureStartQpc.
/// </summary>
public interface IEventStreamProvider : IProvider
{
    Task StartAsync(long captureStartQpc);
    Task StopAsync();
    IAsyncEnumerable<TimestampedEvent> Events { get; }
}
