using FluentAssertions;
using SysAnalyzer.Analysis.Models;

namespace SysAnalyzer.Tests.Unit;

public class FingerprintTests
{
    private static MachineFingerprint CreateBaseline() => new(
        CpuModel: "AMD Ryzen 7 5800X",
        GpuModel: "NVIDIA GeForce RTX 3080",
        TotalRamGb: 32,
        RamConfig: "2x16GB DDR4-3600",
        OsBuild: "22631.3880",
        DisplayConfig: "2560x1440@144Hz",
        StorageConfigHash: "nvme-wd_sn850x",
        GpuDriverMajorVersion: "560",
        MotherboardModel: "ASUS ROG STRIX B550-F"
    );

    [Fact]
    public void ComputeHash_SameInputs_SameHash()
    {
        var a = CreateBaseline();
        var b = CreateBaseline();
        a.ComputeHash().Should().Be(b.ComputeHash());
    }

    [Fact]
    public void ComputeHash_Is12HexChars()
    {
        var hash = CreateBaseline().ComputeHash();
        hash.Should().HaveLength(12);
        hash.Should().MatchRegex("^[0-9a-f]{12}$");
    }

    [Fact]
    public void ComputeHash_CpuChange_DifferentHash()
    {
        var baseline = CreateBaseline();
        var changed = baseline with { CpuModel = "Intel Core i9-14900K" };
        baseline.ComputeHash().Should().NotBe(changed.ComputeHash());
    }

    [Fact]
    public void ComputeHash_GpuChange_DifferentHash()
    {
        var baseline = CreateBaseline();
        var changed = baseline with { GpuModel = "NVIDIA GeForce RTX 4090" };
        baseline.ComputeHash().Should().NotBe(changed.ComputeHash());
    }

    [Fact]
    public void ComputeHash_RamChange_DifferentHash()
    {
        var baseline = CreateBaseline();
        var changed = baseline with { TotalRamGb = 64 };
        baseline.ComputeHash().Should().NotBe(changed.ComputeHash());
    }

    [Fact]
    public void ComputeHash_RamConfigChange_DifferentHash()
    {
        var baseline = CreateBaseline();
        var changed = baseline with { RamConfig = "4x8GB DDR4-3600" };
        baseline.ComputeHash().Should().NotBe(changed.ComputeHash());
    }

    [Fact]
    public void ComputeHash_OsBuildChange_DifferentHash()
    {
        var baseline = CreateBaseline();
        var changed = baseline with { OsBuild = "22631.4000" };
        baseline.ComputeHash().Should().NotBe(changed.ComputeHash());
    }

    [Fact]
    public void ComputeHash_DisplayChange_DifferentHash()
    {
        var baseline = CreateBaseline();
        var changed = baseline with { DisplayConfig = "3840x2160@60Hz" };
        baseline.ComputeHash().Should().NotBe(changed.ComputeHash());
    }

    [Fact]
    public void ComputeHash_StorageChange_DifferentHash()
    {
        var baseline = CreateBaseline();
        var changed = baseline with { StorageConfigHash = "sata-samsung-870" };
        baseline.ComputeHash().Should().NotBe(changed.ComputeHash());
    }

    [Fact]
    public void ComputeHash_DriverChange_DifferentHash()
    {
        var baseline = CreateBaseline();
        var changed = baseline with { GpuDriverMajorVersion = "570" };
        baseline.ComputeHash().Should().NotBe(changed.ComputeHash());
    }

    [Fact]
    public void ComputeHash_MotherboardChange_DifferentHash()
    {
        var baseline = CreateBaseline();
        var changed = baseline with { MotherboardModel = "MSI MAG B550 TOMAHAWK" };
        baseline.ComputeHash().Should().NotBe(changed.ComputeHash());
    }

    [Fact]
    public void Diff_IdenticalFingerprints_EmptyList()
    {
        var a = CreateBaseline();
        var b = CreateBaseline();
        a.Diff(b).Should().BeEmpty();
    }

    [Fact]
    public void Diff_SingleComponentChanged_ReportsIt()
    {
        var a = CreateBaseline();
        var b = a with { TotalRamGb = 64 };
        var diff = a.Diff(b);
        diff.Should().ContainSingle();
        diff[0].Should().Contain("RAM").And.Contain("32").And.Contain("64");
    }

    [Fact]
    public void Diff_MultipleChanges_ReportsAll()
    {
        var a = CreateBaseline();
        var b = a with { CpuModel = "Intel Core i9-14900K", TotalRamGb = 64, GpuDriverMajorVersion = "570" };
        var diff = a.Diff(b);
        diff.Should().HaveCount(3);
    }
}
