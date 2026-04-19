namespace SysAnalyzer.Capture.Providers;

public interface ISystemCheckSource
{
    string? ReadRegistryString(string keyPath, string valueName);
    int? ReadRegistryDword(string keyPath, string valueName);
    string? RunWmiQuery(string className, string propertyName, string? scope = null);
    bool IsServiceRunning(string serviceName);
    long GetDirectorySizeMb(string path);
    string GetActiveAvProduct();
}
