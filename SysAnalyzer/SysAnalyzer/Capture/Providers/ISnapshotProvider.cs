using SysAnalyzer.Capture;

namespace SysAnalyzer.Capture.Providers;

/// <summary>
/// Captures static data once at session start or end.
/// Used by: HardwareInventoryProvider, WmiExtendedProvider, WindowsDeepCheckProvider.
/// </summary>
public interface ISnapshotProvider : IProvider
{
    Task<SnapshotData> CaptureAsync();
}
