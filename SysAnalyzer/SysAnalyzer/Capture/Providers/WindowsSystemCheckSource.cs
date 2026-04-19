using System.Management;
using System.ServiceProcess;
using Microsoft.Win32;

namespace SysAnalyzer.Capture.Providers;

public sealed class WindowsSystemCheckSource : ISystemCheckSource
{
    public string? ReadRegistryString(string keyPath, string valueName)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(keyPath)
                         ?? Registry.CurrentUser.OpenSubKey(keyPath);
            return key?.GetValue(valueName)?.ToString();
        }
        catch { return null; }
    }

    public int? ReadRegistryDword(string keyPath, string valueName)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(keyPath)
                         ?? Registry.CurrentUser.OpenSubKey(keyPath);
            var val = key?.GetValue(valueName);
            return val is int i ? i : null;
        }
        catch { return null; }
    }

    public string? RunWmiQuery(string className, string propertyName, string? scope = null)
    {
        try
        {
            var s = scope ?? @"root\cimv2";
            using var searcher = new ManagementObjectSearcher(s, $"SELECT {propertyName} FROM {className}");
            foreach (var obj in searcher.Get())
            {
                return obj[propertyName]?.ToString();
            }
            return null;
        }
        catch { return null; }
    }

    public bool IsServiceRunning(string serviceName)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            return sc.Status == ServiceControllerStatus.Running;
        }
        catch { return false; }
    }

    public long GetDirectorySizeMb(string path)
    {
        try
        {
            var expanded = Environment.ExpandEnvironmentVariables(path);
            if (!Directory.Exists(expanded)) return 0;
            var size = new DirectoryInfo(expanded)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Sum(f => f.Length);
            return size / (1024 * 1024);
        }
        catch { return 0; }
    }

    public string GetActiveAvProduct()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\SecurityCenter2", "SELECT displayName FROM AntivirusProduct");
            foreach (var obj in searcher.Get())
            {
                return obj["displayName"]?.ToString() ?? "Unknown";
            }
            return "None detected";
        }
        catch { return "Unknown"; }
    }
}
