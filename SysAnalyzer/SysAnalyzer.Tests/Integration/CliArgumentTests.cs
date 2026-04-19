using SysAnalyzer.Cli;

namespace SysAnalyzer.Tests.Integration;

public class CliArgumentTests
{
    [Fact]
    public void Parse_NoArgs_DefaultValues()
    {
        var options = CliParser.Parse(Array.Empty<string>());
        Assert.Equal("gaming", options.Profile);
        Assert.Null(options.Label);
        Assert.Null(options.Process);
        Assert.False(options.Elevate);
        Assert.False(options.NoPresentmon);
        Assert.False(options.NoEtw);
        Assert.False(options.NoLive);
        Assert.False(options.Version);
        Assert.False(options.Help);
    }

    [Fact]
    public void Parse_Profile_SetsValue()
    {
        var options = CliParser.Parse(new[] { "--profile", "compiling" });
        Assert.Equal("compiling", options.Profile);
    }

    [Fact]
    public void Parse_Label_SetsValue()
    {
        var options = CliParser.Parse(new[] { "--label", "my-test-run" });
        Assert.Equal("my-test-run", options.Label);
    }

    [Fact]
    public void Parse_Process_SetsValue()
    {
        var options = CliParser.Parse(new[] { "--process", "game.exe" });
        Assert.Equal("game.exe", options.Process);
    }

    [Fact]
    public void Parse_Config_SetsValue()
    {
        var options = CliParser.Parse(new[] { "--config", "custom.yaml" });
        Assert.Equal("custom.yaml", options.ConfigPath);
    }

    [Fact]
    public void Parse_Output_SetsValue()
    {
        var options = CliParser.Parse(new[] { "--output", "/tmp/reports" });
        Assert.Equal("/tmp/reports", options.OutputDir);
    }

    [Fact]
    public void Parse_Interval_SetsValue()
    {
        var options = CliParser.Parse(new[] { "--interval", "500" });
        Assert.Equal(500, options.Interval);
    }

    [Fact]
    public void Parse_Duration_SetsValue()
    {
        var options = CliParser.Parse(new[] { "--duration", "60" });
        Assert.Equal(60, options.Duration);
    }

    [Fact]
    public void Parse_BooleanFlags()
    {
        var options = CliParser.Parse(new[] { "--elevate", "--no-presentmon", "--no-etw", "--no-live", "--csv", "--etl" });
        Assert.True(options.Elevate);
        Assert.True(options.NoPresentmon);
        Assert.True(options.NoEtw);
        Assert.True(options.NoLive);
        Assert.True(options.Csv);
        Assert.True(options.Etl);
    }

    [Fact]
    public void Parse_Version()
    {
        var options = CliParser.Parse(new[] { "--version" });
        Assert.True(options.Version);
    }

    [Fact]
    public void Parse_Help()
    {
        var options = CliParser.Parse(new[] { "--help" });
        Assert.True(options.Help);
    }

    [Fact]
    public void Parse_HelpShort()
    {
        var options = CliParser.Parse(new[] { "-h" });
        Assert.True(options.Help);
    }

    [Fact]
    public void Parse_Compare_SetsValue()
    {
        var options = CliParser.Parse(new[] { "--compare", "baseline.json" });
        Assert.Equal("baseline.json", options.Compare);
    }

    [Fact]
    public void Parse_UnknownArg_Throws()
    {
        Assert.Throws<ArgumentException>(() => CliParser.Parse(new[] { "--unknown" }));
    }

    [Fact]
    public void Parse_MissingValue_Throws()
    {
        Assert.Throws<ArgumentException>(() => CliParser.Parse(new[] { "--profile" }));
    }

    [Fact]
    public void Parse_MultipleArgs_Combined()
    {
        var options = CliParser.Parse(new[]
        {
            "--profile", "gaming",
            "--label", "test-run",
            "--interval", "500",
            "--duration", "120",
            "--no-live"
        });
        Assert.Equal("gaming", options.Profile);
        Assert.Equal("test-run", options.Label);
        Assert.Equal(500, options.Interval);
        Assert.Equal(120, options.Duration);
        Assert.True(options.NoLive);
    }

    [Fact]
    public void GetHelpText_NotEmpty()
    {
        var help = CliParser.GetHelpText();
        Assert.NotEmpty(help);
        Assert.Contains("--profile", help);
        Assert.Contains("--help", help);
        Assert.Contains("--elevate", help);
    }
}
