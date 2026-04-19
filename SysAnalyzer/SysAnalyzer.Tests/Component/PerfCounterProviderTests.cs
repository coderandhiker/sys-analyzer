using SysAnalyzer.Capture;
using SysAnalyzer.Capture.Providers;

namespace SysAnalyzer.Tests.Component;

public class PerfCounterProviderTests
{
    private class FakeCounterHandle : IPerfCounterHandle
    {
        private readonly float _value;
        public FakeCounterHandle(float value) => _value = value;
        public float NextValue() => _value;
        public void Dispose() { }
    }

    private class ThrowingCounterHandle : IPerfCounterHandle
    {
        public float NextValue() => throw new InvalidOperationException("Counter read failed");
        public void Dispose() { }
    }

    private class FakePerfCounterFactory : IPerfCounterFactory
    {
        private readonly Dictionary<string, float> _values = new();
        private readonly HashSet<string> _missing = new();

        public void SetValue(string category, string counter, float value)
            => _values[$"{category}\\{counter}"] = value;

        public void SetMissing(string category, string counter)
            => _missing.Add($"{category}\\{counter}");

        public IPerfCounterHandle? TryCreate(string category, string counter, string instance)
        {
            var key = $"{category}\\{counter}";
            if (_missing.Contains(key)) return null;
            return new FakeCounterHandle(_values.GetValueOrDefault(key, 0f));
        }
    }

    private class ThrowingFactory : IPerfCounterFactory
    {
        public IPerfCounterHandle? TryCreate(string category, string counter, string instance) => null;
    }

    [Fact]
    public async Task Init_AllCountersAvailable_Active()
    {
        var factory = new FakePerfCounterFactory();
        var provider = new PerformanceCounterProvider(factory);
        var health = await provider.InitAsync();

        Assert.Equal(ProviderStatus.Active, health.Status);
        Assert.Equal(health.MetricsExpected, health.MetricsAvailable);
    }

    [Fact]
    public async Task Init_SomeCountersMissing_Degraded()
    {
        var factory = new FakePerfCounterFactory();
        factory.SetMissing("GPU Engine", "Utilization Percentage");
        factory.SetMissing("GPU Process Memory", "Dedicated Usage");

        var provider = new PerformanceCounterProvider(factory);
        var health = await provider.InitAsync();

        Assert.Equal(ProviderStatus.Degraded, health.Status);
        Assert.True(health.MetricsAvailable < health.MetricsExpected);
        Assert.NotNull(health.DegradationReason);
        Assert.Contains("GPU", health.DegradationReason!);
    }

    [Fact]
    public async Task Init_NoCounters_Failed()
    {
        var factory = new ThrowingFactory();
        var provider = new PerformanceCounterProvider(factory);
        var health = await provider.InitAsync();

        Assert.Equal(ProviderStatus.Failed, health.Status);
        Assert.Equal(0, health.MetricsAvailable);
    }

    [Fact]
    public async Task Poll_ReturnsBatch_WithCpuValue()
    {
        var factory = new FakePerfCounterFactory();
        factory.SetValue("Processor", "% Processor Time", 75.5f);

        var provider = new PerformanceCounterProvider(factory);
        await provider.InitAsync();

        var batch = provider.Poll(0);
        Assert.False(batch.IsEmpty);
        Assert.Equal(75.5, batch.TotalCpuPercent, precision: 1);
    }

    [Fact]
    public async Task Poll_MemoryMetrics_Populated()
    {
        var factory = new FakePerfCounterFactory();
        factory.SetValue("Memory", "Available MBytes", 8000f);
        factory.SetValue("Memory", "% Committed Bytes In Use", 65f);

        var provider = new PerformanceCounterProvider(factory);
        await provider.InitAsync();

        var batch = provider.Poll(0);
        Assert.Equal(8000, batch.AvailableMemoryMb, precision: 0);
        Assert.Equal(65, batch.CommittedBytesInUsePercent, precision: 0);
    }

    [Fact]
    public async Task Poll_MissingGpuCounters_NaN()
    {
        var factory = new FakePerfCounterFactory();
        factory.SetMissing("GPU Engine", "Utilization Percentage");
        factory.SetMissing("GPU Process Memory", "Dedicated Usage");

        var provider = new PerformanceCounterProvider(factory);
        await provider.InitAsync();

        var batch = provider.Poll(0);
        Assert.True(double.IsNaN(batch.GpuUtilizationPercent));
    }

    [Fact]
    public void Name_IsPerformanceCounters()
    {
        var provider = new PerformanceCounterProvider(new FakePerfCounterFactory());
        Assert.Equal("PerformanceCounters", provider.Name);
    }

    [Fact]
    public void RequiredTier_IsTier1()
    {
        var provider = new PerformanceCounterProvider(new FakePerfCounterFactory());
        Assert.Equal(ProviderTier.Tier1, provider.RequiredTier);
    }
}
