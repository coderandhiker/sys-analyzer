namespace SysAnalyzer.Capture.Providers;

public interface IPerfCounterHandle : IDisposable
{
    float NextValue();
}

public interface IPerfCounterFactory
{
    IPerfCounterHandle? TryCreate(string category, string counter, string instance);
}
