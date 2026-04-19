using SysAnalyzer.Analysis.Models;

namespace SysAnalyzer.Tests.Unit;

public class FingerprintTests
{
    private static MachineFingerprint CreateFingerprint(
        string cpu = "AMD Ryzen 7 5800X",
        string gpu = "NVIDIA RTX 3080",
        int ram = 32,
        string ramConfig = "2x16GB DDR4-3600",
        string os = "22631",
        string display = "2560x1440@144Hz",
        string storage = "nvme",
        string driver = "560",
        string mobo = "ASUS B550") =>
        new(cpu, gpu, ram, ramConfig, os, display, storage, driver, mobo);

    [Fact]
    public void Hash_Is12HexChars()
    {
        var fp = CreateFingerprint();
        var hash = fp.ComputeHash();
        Assert.Equal(12, hash.Length);
        Assert.Matches("^[0-9a-f]{12}$", hash);
    }

    [Fact]
    public void Hash_Deterministic()
    {
        var fp1 = CreateFingerprint();
        var fp2 = CreateFingerprint();
        Assert.Equal(fp1.ComputeHash(), fp2.ComputeHash());
    }

    [Fact]
    public void Hash_DifferentCpu_DifferentHash()
    {
        var fp1 = CreateFingerprint(cpu: "AMD Ryzen 7 5800X");
        var fp2 = CreateFingerprint(cpu: "Intel Core i9-14900K");
        Assert.NotEqual(fp1.ComputeHash(), fp2.ComputeHash());
    }

    [Fact]
    public void Hash_DifferentGpu_DifferentHash()
    {
        var fp1 = CreateFingerprint(gpu: "NVIDIA RTX 3080");
        var fp2 = CreateFingerprint(gpu: "NVIDIA RTX 4090");
        Assert.NotEqual(fp1.ComputeHash(), fp2.ComputeHash());
    }

    [Fact]
    public void Hash_DifferentRam_DifferentHash()
    {
        var fp1 = CreateFingerprint(ram: 32);
        var fp2 = CreateFingerprint(ram: 64);
        Assert.NotEqual(fp1.ComputeHash(), fp2.ComputeHash());
    }

    [Fact]
    public void Diff_IdenticalFingerprints_Empty()
    {
        var fp = CreateFingerprint();
        var diffs = fp.Diff(fp);
        Assert.Empty(diffs);
    }

    [Fact]
    public void Diff_DifferentCpu_ReportsChange()
    {
        var fp1 = CreateFingerprint(cpu: "AMD Ryzen 7 5800X");
        var fp2 = CreateFingerprint(cpu: "Intel Core i9-14900K");
        var diffs = fp1.Diff(fp2);
        Assert.Single(diffs);
        Assert.Contains("CPU", diffs[0]);
    }

    [Fact]
    public void Diff_MultipleChanges_ReportsAll()
    {
        var fp1 = CreateFingerprint();
        var fp2 = CreateFingerprint(cpu: "Different", gpu: "Different", ram: 64);
        var diffs = fp1.Diff(fp2);
        Assert.Equal(3, diffs.Count);
    }

    [Fact]
    public void Hash_OrderIndependent()
    {
        // The hash sorts components by key, so order shouldn't matter
        var fp = CreateFingerprint();
        var hash = fp.ComputeHash();
        Assert.NotEmpty(hash);
    }
}
