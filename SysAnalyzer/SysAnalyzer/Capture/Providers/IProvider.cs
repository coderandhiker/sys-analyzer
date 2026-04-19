namespace SysAnalyzer.Capture.Providers;

/// <summary>
/// Base provider interface. All data sources implement this.
/// </summary>
public interface IProvider : IDisposable
{
    string Name { get; }
    ProviderTier RequiredTier { get; }
    Task<ProviderHealth> InitAsync();
    ProviderHealth Health { get; }
}
