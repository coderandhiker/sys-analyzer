using SysAnalyzer.Capture.Providers;

namespace SysAnalyzer.Capture;

/// <summary>
/// Tracks health status for all providers. Aggregates individual ProviderHealth records.
/// </summary>
public class SensorHealthMatrix
{
    private readonly Dictionary<string, ProviderHealth> _health = new();

    public void Register(string providerName, ProviderHealth health)
    {
        _health[providerName] = health;
    }

    public IReadOnlyDictionary<string, ProviderHealth> Providers => _health;

    public ProviderTier OverallTier =>
        _health.Any(kv => IsTier2Provider(kv.Key) && kv.Value.Status == ProviderStatus.Active)
            ? ProviderTier.Tier2
            : ProviderTier.Tier1;

    public IReadOnlyList<string> DegradedProviders =>
        _health.Where(kv => kv.Value.Status == ProviderStatus.Degraded)
               .Select(kv => kv.Key)
               .ToList();

    public IReadOnlyList<string> FailedProviders =>
        _health.Where(kv => kv.Value.Status == ProviderStatus.Failed)
               .Select(kv => kv.Key)
               .ToList();

    private static bool IsTier2Provider(string name) =>
        name.Contains("LibreHardware", StringComparison.OrdinalIgnoreCase);
}
