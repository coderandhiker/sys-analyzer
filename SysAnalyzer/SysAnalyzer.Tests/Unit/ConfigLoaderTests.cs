using SysAnalyzer.Config;

namespace SysAnalyzer.Tests.Unit;

public class ConfigLoaderTests
{
    [Fact]
    public void Load_ValidYaml_Succeeds()
    {
        var config = ConfigLoader.Load();
        Assert.NotNull(config);
        Assert.Equal(1000, config.Capture.PollIntervalMs);
        Assert.True(config.Profiles.ContainsKey("gaming"));
        Assert.NotEmpty(config.Recommendations);
    }

    [Fact]
    public void Load_DefaultConfig_HasExpectedProfiles()
    {
        var config = ConfigLoader.Load();
        Assert.True(config.Profiles.ContainsKey("gaming"));
        Assert.True(config.Profiles.ContainsKey("compiling"));
        Assert.True(config.Profiles.ContainsKey("general_interactive"));
    }

    [Fact]
    public void Load_DefaultConfig_HasExpectedThresholds()
    {
        var config = ConfigLoader.Load();
        Assert.True(config.Thresholds.Cpu.ContainsKey("load_moderate"));
        Assert.True(config.Thresholds.Memory.ContainsKey("utilization_moderate"));
    }

    [Fact]
    public void Load_DefaultConfig_HasRecommendations()
    {
        var config = ConfigLoader.Load();
        Assert.True(config.Recommendations.Count > 0);
        Assert.All(config.Recommendations, r => Assert.False(string.IsNullOrEmpty(r.Id)));
    }

    [Fact]
    public void Load_ExplicitPath_FileNotFound_Throws()
    {
        Assert.Throws<FileNotFoundException>(() => ConfigLoader.Load("nonexistent.yaml"));
    }

    [Fact]
    public void Load_ValidConfig_ParsesCapture()
    {
        var config = ConfigLoader.Load();
        Assert.Equal(30, config.Capture.MinCaptureDurationSec);
        Assert.Equal(28800, config.Capture.MaxCaptureDurationSec);
        Assert.True(config.Capture.PresentmonEnabled);
        Assert.True(config.Capture.EtwEnabled);
    }

    [Fact]
    public void Load_ValidConfig_ParsesOutput()
    {
        var config = ConfigLoader.Load();
        Assert.Equal("./reports", config.Output.Directory);
        Assert.Contains("sysanalyzer", config.Output.FilenameFormat);
    }

    [Fact]
    public void Load_ValidConfig_ParsesBaselines()
    {
        var config = ConfigLoader.Load();
        Assert.True(config.Baselines.AutoSave);
        Assert.Equal(50, config.Baselines.MaxStored);
    }
}
