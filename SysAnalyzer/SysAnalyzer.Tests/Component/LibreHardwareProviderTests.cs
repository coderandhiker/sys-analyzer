using SysAnalyzer.Capture;
using SysAnalyzer.Capture.Providers;

namespace SysAnalyzer.Tests.Component;

/// <summary>
/// Component tests for LibreHardwareProvider failure modes.
/// Since we cannot load actual LHM in tests (no admin, no driver), these test
/// the provider's behavior via its public contract.
/// </summary>
public class LibreHardwareProviderTests
{
    [Fact]
    public async Task NotElevated_ReturnsUnavailable_NoCrash()
    {
        // When running tests without admin, the provider should return Unavailable
        using var provider = new LibreHardwareProvider();
        var health = await provider.InitAsync();

        // In test environment (non-admin), expect Unavailable
        if (!LibreHardwareProvider.IsElevated())
        {
            Assert.Equal(ProviderStatus.Unavailable, health.Status);
            Assert.Contains("Not elevated", health.DegradationReason);
            Assert.Equal(0, health.MetricsAvailable);
        }
        // If tests somehow run elevated, it should be Active or Failed (driver issue)
        else
        {
            Assert.True(health.Status == ProviderStatus.Active || health.Status == ProviderStatus.Failed);
        }
    }

    [Fact]
    public async Task NotElevated_PollReturnsEmpty()
    {
        using var provider = new LibreHardwareProvider();
        await provider.InitAsync();

        if (!LibreHardwareProvider.IsElevated())
        {
            var batch = provider.Poll(1000);
            Assert.True(batch.IsEmpty);
        }
    }

    [Fact]
    public void Name_IsLibreHardwareMonitor()
    {
        using var provider = new LibreHardwareProvider();
        Assert.Equal("LibreHardwareMonitor", provider.Name);
    }

    [Fact]
    public void RequiredTier_IsTier2()
    {
        using var provider = new LibreHardwareProvider();
        Assert.Equal(ProviderTier.Tier2, provider.RequiredTier);
    }

    [Fact]
    public async Task Dispose_DoesNotThrow()
    {
        var provider = new LibreHardwareProvider();
        await provider.InitAsync();
        var ex = Record.Exception(() => provider.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public async Task DoubleDispose_DoesNotThrow()
    {
        var provider = new LibreHardwareProvider();
        await provider.InitAsync();
        provider.Dispose();
        var ex = Record.Exception(() => provider.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public async Task HealthMatrix_ShowsUnavailableWhenNotElevated()
    {
        if (LibreHardwareProvider.IsElevated())
            return; // skip on admin

        using var provider = new LibreHardwareProvider();
        await provider.InitAsync();

        var matrix = new SensorHealthMatrix();
        matrix.Register(provider.Name, provider.Health);

        Assert.Equal(ProviderTier.Tier1, matrix.OverallTier);
    }

    [Fact]
    public async Task Poll_AfterInit_NeverCrashes()
    {
        using var provider = new LibreHardwareProvider();
        await provider.InitAsync();

        // Multiple polls should never throw
        for (int i = 0; i < 10; i++)
        {
            var ex = Record.Exception(() => provider.Poll(i * 1000));
            Assert.Null(ex);
        }
    }
}
