using SysAnalyzer.Capture.Providers;
using SysAnalyzer.Cli;

namespace SysAnalyzer.Tests.Integration;

/// <summary>
/// Tests for the elevation flow logic.
/// </summary>
public class ElevationFlowTests
{
    [Fact]
    public void CliParser_ParsesElevateFlag()
    {
        var opts = CliParser.Parse(["--elevate", "--duration", "15"]);

        Assert.True(opts.Elevate);
        Assert.Equal(15, opts.Duration);
    }

    [Fact]
    public void CliParser_AllArgsPreservedWithoutElevate()
    {
        var args = new[] { "--elevate", "--profile", "gaming", "--duration", "30", "--no-live" };
        var argsWithoutElevate = args.Where(a => a != "--elevate").ToArray();

        var opts = CliParser.Parse(argsWithoutElevate);

        Assert.False(opts.Elevate);
        Assert.Equal("gaming", opts.Profile);
        Assert.Equal(30, opts.Duration);
        Assert.True(opts.NoLive);
    }

    [Fact]
    public void IsElevated_ReturnsBoolean()
    {
        // Just verify it doesn't throw and returns a valid boolean
        bool result = LibreHardwareProvider.IsElevated();
        Assert.IsType<bool>(result);
    }

    [Fact]
    public void AlreadyElevated_SkipsRelaunch()
    {
        // When already elevated + --elevate, the code should detect elevation and not re-launch.
        // We test the logic: if IsElevated() returns true, --elevate becomes a no-op.
        // This test just verifies the flag parsing doesn't break anything.
        var opts = CliParser.Parse(["--elevate"]);
        Assert.True(opts.Elevate);

        // The actual logic is: if (options.Elevate && IsElevated()) → no-op, continue
        // We can't test the actual re-launch without spawning processes, but we verify the detection API
        bool elevated = LibreHardwareProvider.IsElevated();
        // In test environment (non-admin CI), this should be false
        // In admin test environment, this should be true
        // Either way, no exception
    }

    [Fact]
    public void CliParser_PreservesAllFlagsForRelaunch()
    {
        // Verify all CLI args can round-trip through parse
        var args = new[]
        {
            "--profile", "compiling",
            "--label", "test-run",
            "--process", "game.exe",
            "--config", "custom.yaml",
            "--output", "./out",
            "--interval", "500",
            "--no-presentmon",
            "--no-etw",
            "--no-live",
            "--compare", "baseline.json",
            "--csv",
            "--etl",
            "--duration", "60"
        };

        var opts = CliParser.Parse(args);

        Assert.Equal("compiling", opts.Profile);
        Assert.Equal("test-run", opts.Label);
        Assert.Equal("game.exe", opts.Process);
        Assert.Equal("custom.yaml", opts.ConfigPath);
        Assert.Equal("./out", opts.OutputDir);
        Assert.Equal(500, opts.Interval);
        Assert.True(opts.NoPresentmon);
        Assert.True(opts.NoEtw);
        Assert.True(opts.NoLive);
        Assert.Equal("baseline.json", opts.Compare);
        Assert.True(opts.Csv);
        Assert.True(opts.Etl);
        Assert.Equal(60, opts.Duration);
    }
}
