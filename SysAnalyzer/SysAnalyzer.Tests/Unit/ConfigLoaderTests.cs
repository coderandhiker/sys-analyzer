using FluentAssertions;
using SysAnalyzer.Config;

namespace SysAnalyzer.Tests.Unit;

public class ConfigLoaderTests
{
    [Fact]
    public void Load_ValidYaml_Succeeds()
    {
        // The embedded default config should load without error
        var config = ConfigLoader.Load();
        config.Should().NotBeNull();
        config.Capture.PollIntervalMs.Should().Be(1000);
        config.Profiles.Should().ContainKey("gaming");
        config.Recommendations.Should().NotBeEmpty();
    }

    [Fact]
    public void Load_DefaultConfig_HasExpectedProfiles()
    {
        var config = ConfigLoader.Load();
        config.Profiles.Should().ContainKey("gaming");
        config.Profiles.Should().ContainKey("compiling");
        config.Profiles.Should().ContainKey("general_interactive");
    }

    [Fact]
    public void Load_DefaultConfig_HasExpectedThresholds()
    {
        var config = ConfigLoader.Load();
        config.Thresholds.Cpu.Should().ContainKey("load_moderate");
        config.Thresholds.Memory.Should().ContainKey("utilization_moderate");
    }

    [Fact]
    public void Load_DefaultConfig_HasRecommendations()
    {
        var config = ConfigLoader.Load();
        config.Recommendations.Should().HaveCountGreaterThan(0);
        config.Recommendations.Should().OnlyContain(r => !string.IsNullOrEmpty(r.Id));
    }

    [Fact]
    public void Load_ExplicitPath_FileNotFound_Throws()
    {
        var act = () => ConfigLoader.Load("nonexistent.yaml");
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void Load_ValidConfig_ParsesCapture()
    {
        var config = ConfigLoader.Load();
        config.Capture.MinCaptureDurationSec.Should().Be(30);
        config.Capture.MaxCaptureDurationSec.Should().Be(28800);
        config.Capture.PresentmonEnabled.Should().BeTrue();
        config.Capture.EtwEnabled.Should().BeTrue();
    }

    [Fact]
    public void Load_ValidConfig_ParsesOutput()
    {
        var config = ConfigLoader.Load();
        config.Output.Directory.Should().Be("./reports");
        config.Output.FilenameFormat.Should().Contain("sysanalyzer");
    }

    [Fact]
    public void Load_ValidConfig_ParsesBaselines()
    {
        var config = ConfigLoader.Load();
        config.Baselines.AutoSave.Should().BeTrue();
        config.Baselines.MaxStored.Should().Be(50);
    }
}
