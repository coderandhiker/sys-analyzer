using System.Security.Cryptography;
using System.Text;

namespace SysAnalyzer.Analysis.Models;

/// <summary>
/// Machine identity for reliable before/after comparison (§9).
/// 9 components; SHA-256 hash (first 12 hex chars); sorted before hashing.
/// </summary>
public record MachineFingerprint(
    string CpuModel,
    string GpuModel,
    int TotalRamGb,
    string RamConfig,
    string OsBuild,
    string DisplayConfig,
    string StorageConfigHash,
    string GpuDriverMajorVersion,
    string MotherboardModel
)
{
    /// <summary>
    /// Compute a deterministic hash of all 9 components.
    /// Components are sorted alphabetically before hashing to ensure order-independence.
    /// Returns the first 12 hex characters of the SHA-256 hash.
    /// </summary>
    public string ComputeHash()
    {
        var components = new[]
        {
            $"cpu:{CpuModel}",
            $"display:{DisplayConfig}",
            $"gpu:{GpuModel}",
            $"gpudriver:{GpuDriverMajorVersion}",
            $"motherboard:{MotherboardModel}",
            $"osbuild:{OsBuild}",
            $"ram:{TotalRamGb}",
            $"ramconfig:{RamConfig}",
            $"storage:{StorageConfigHash}"
        };

        // Already sorted by key prefix; explicit sort for safety
        Array.Sort(components, StringComparer.Ordinal);

        var combined = string.Join("|", components);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
        return Convert.ToHexStringLower(hashBytes)[..12];
    }

    /// <summary>
    /// List which components differ between this fingerprint and another.
    /// </summary>
    public List<string> Diff(MachineFingerprint other)
    {
        var changes = new List<string>();

        if (CpuModel != other.CpuModel)
            changes.Add($"CPU: {CpuModel} → {other.CpuModel}");
        if (GpuModel != other.GpuModel)
            changes.Add($"GPU: {GpuModel} → {other.GpuModel}");
        if (TotalRamGb != other.TotalRamGb)
            changes.Add($"RAM: {TotalRamGb}GB → {other.TotalRamGb}GB");
        if (RamConfig != other.RamConfig)
            changes.Add($"RAM Config: {RamConfig} → {other.RamConfig}");
        if (OsBuild != other.OsBuild)
            changes.Add($"OS Build: {OsBuild} → {other.OsBuild}");
        if (DisplayConfig != other.DisplayConfig)
            changes.Add($"Display: {DisplayConfig} → {other.DisplayConfig}");
        if (StorageConfigHash != other.StorageConfigHash)
            changes.Add($"Storage: changed");
        if (GpuDriverMajorVersion != other.GpuDriverMajorVersion)
            changes.Add($"GPU Driver: {GpuDriverMajorVersion} → {other.GpuDriverMajorVersion}");
        if (MotherboardModel != other.MotherboardModel)
            changes.Add($"Motherboard: {MotherboardModel} → {other.MotherboardModel}");

        return changes;
    }
}
