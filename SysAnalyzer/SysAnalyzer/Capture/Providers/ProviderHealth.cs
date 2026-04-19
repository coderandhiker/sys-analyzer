namespace SysAnalyzer.Capture.Providers;

/// <summary>Tier 1 = no admin required. Tier 2 = admin/elevated required.</summary>
public enum ProviderTier
{
    Tier1,
    Tier2
}

/// <summary>Runtime status of a provider.</summary>
public enum ProviderStatus
{
    Active,
    Degraded,
    Unavailable,
    Failed
}

/// <summary>Health report for a single provider.</summary>
public record ProviderHealth(
    ProviderStatus Status,
    string? DegradationReason,
    int MetricsAvailable,
    int MetricsExpected,
    int EventsLost
);
