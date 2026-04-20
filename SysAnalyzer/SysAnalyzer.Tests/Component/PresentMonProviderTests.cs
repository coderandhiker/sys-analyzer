using System.Diagnostics;
using SysAnalyzer.Capture;
using SysAnalyzer.Capture.Providers;

namespace SysAnalyzer.Tests.Component;

/// <summary>
/// Fake process launcher that provides canned CSV output via a real process (type.exe on stdin).
/// </summary>
public sealed class FakePresentMonLauncher : IPresentMonProcessLauncher
{
    private readonly string? _csvContent;
    private readonly bool _binaryExists;
    private readonly int _exitCode;

    public FakePresentMonLauncher(
        string? csvContent = null,
        bool binaryExists = true,
        int exitCode = 0)
    {
        _csvContent = csvContent;
        _binaryExists = binaryExists;
        _exitCode = exitCode;
    }

    public string GetBinaryPath() => "FakePresentMon.exe";
    public bool BinaryExists(string path) => _binaryExists;
    public bool TryAcquireBinary(string path) => false;

    public Process? Start(string path, string arguments)
    {
        if (!_binaryExists) return null;

        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, _csvContent ?? "");

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c type \"{tempFile}\"" + (_exitCode != 0 ? $" & exit /b {_exitCode}" : ""),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var process = Process.Start(psi);

        if (process != null)
        {
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) =>
            {
                try { File.Delete(tempFile); } catch { }
            };
        }

        return process;
    }
}

public class PresentMonProviderTests
{
    private const string StandardHeader = "Application,ProcessID,SwapChainAddress,Runtime,SyncInterval,PresentFlags,AllowsTearing,TimeInSeconds,msBetweenPresents,msBetweenDisplayChange,msInPresentAPI,msUntilRenderComplete,msUntilDisplayed,msCPUBusy,msGPUBusy,VideoPresentDuration,QPCTime,Dropped,PresentMode";

    public PresentMonProviderTests()
    {
        QpcTimestamp.SetCaptureEpoch(Stopwatch.GetTimestamp(), DateTime.UtcNow);
    }

    [Fact]
    public async Task BinaryNotFound_HealthUnavailable()
    {
        var launcher = new FakePresentMonLauncher(binaryExists: false);
        var provider = new PresentMonProvider(launcher: launcher);

        var health = await provider.InitAsync();

        Assert.Equal(ProviderStatus.Unavailable, health.Status);
        Assert.Equal(ProviderStatus.Unavailable, provider.Health.Status);
    }

    [Fact]
    public async Task BinaryFound_HealthActive()
    {
        var launcher = new FakePresentMonLauncher(binaryExists: true);
        var provider = new PresentMonProvider(launcher: launcher);

        var health = await provider.InitAsync();

        Assert.Equal(ProviderStatus.Active, health.Status);
    }

    [Fact]
    public async Task NormalStream_ProducesFrameTimeSamples()
    {
        var csv = StandardHeader + "\n"
            + "TestGame.exe,1234,0x1,DXGI,1,0,0,0.500,16.67,16.67,0.45,2.10,16.50,8.20,12.30,16.50,500000000,0,Hardware: Independent Flip\n"
            + "TestGame.exe,1234,0x1,DXGI,1,0,0,0.517,16.70,16.70,0.43,2.05,16.55,8.10,12.50,16.55,517000000,0,Hardware: Independent Flip\n"
            + "TestGame.exe,1234,0x1,DXGI,1,0,0,0.534,16.65,16.65,0.44,2.08,16.48,8.15,12.20,16.48,534000000,0,Hardware: Independent Flip\n";

        var launcher = new FakePresentMonLauncher(csvContent: csv);
        var provider = new PresentMonProvider(launcher: launcher);
        await provider.InitAsync();

        var epoch = Stopwatch.GetTimestamp();
        await provider.StartAsync(epoch);

        var samples = new List<FrameTimeSample>();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await foreach (var evt in provider.Events.WithCancellation(cts.Token))
        {
            if (evt is FrameTimeSample sample)
                samples.Add(sample);
        }

        await provider.StopAsync();

        Assert.Equal(3, samples.Count);
        Assert.Equal("TestGame.exe", samples[0].ApplicationName);
        Assert.InRange(samples[0].FrameTimeMs, 16.66, 16.68);
        Assert.InRange(samples[0].CpuBusyMs, 8.19, 8.21);
        Assert.InRange(samples[0].GpuBusyMs, 12.29, 12.31);
        Assert.False(samples[0].Dropped);
        Assert.Equal("Hardware: Independent Flip", samples[0].PresentMode);
    }

    [Fact]
    public async Task EmptyStream_HealthDegraded()
    {
        var csv = StandardHeader + "\n";

        var launcher = new FakePresentMonLauncher(csvContent: csv);
        var provider = new PresentMonProvider(launcher: launcher);
        await provider.InitAsync();

        var epoch = Stopwatch.GetTimestamp();
        await provider.StartAsync(epoch);

        var samples = new List<FrameTimeSample>();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await foreach (var evt in provider.Events.WithCancellation(cts.Token))
        {
            if (evt is FrameTimeSample sample)
                samples.Add(sample);
        }

        await provider.StopAsync();

        Assert.Empty(samples);
        Assert.Equal(ProviderStatus.Degraded, provider.Health.Status);
    }

    [Fact]
    public async Task TwoApps_HighestFrameCountSelected()
    {
        var csv = StandardHeader + "\n"
            + "GameApp.exe,1234,0x1,DXGI,1,0,0,0.500,16.67,16.67,0.45,2.10,16.50,8.20,12.30,16.50,500000000,0,Hardware: Independent Flip\n"
            + "Discord.exe,5678,0x2,DXGI,1,0,0,0.510,33.33,33.33,0.30,1.80,33.00,2.50,3.00,33.00,510000000,0,Composed: Flip\n"
            + "GameApp.exe,1234,0x1,DXGI,1,0,0,0.517,16.70,16.70,0.43,2.05,16.55,8.10,12.50,16.55,517000000,0,Hardware: Independent Flip\n"
            + "GameApp.exe,1234,0x1,DXGI,1,0,0,0.534,16.65,16.65,0.44,2.08,16.48,8.15,12.20,16.48,534000000,0,Hardware: Independent Flip\n"
            + "Discord.exe,5678,0x2,DXGI,1,0,0,0.543,33.35,33.35,0.28,1.78,33.05,2.55,3.10,33.05,543000000,0,Composed: Flip\n"
            + "GameApp.exe,1234,0x1,DXGI,1,0,0,0.550,16.68,16.68,0.46,2.12,16.52,8.25,12.40,16.52,550000000,0,Hardware: Independent Flip\n"
            + "GameApp.exe,1234,0x1,DXGI,1,0,0,0.567,16.72,16.72,0.42,2.00,16.60,8.05,12.60,16.60,567000000,0,Hardware: Independent Flip\n"
            + "Discord.exe,5678,0x2,DXGI,1,0,0,0.576,33.30,33.30,0.31,1.85,32.95,2.48,3.05,32.95,576000000,0,Composed: Flip\n"
            + "GameApp.exe,1234,0x1,DXGI,1,0,0,0.584,16.66,16.66,0.47,2.15,16.45,8.30,12.10,16.45,584000000,0,Hardware: Independent Flip\n"
            + "GameApp.exe,1234,0x1,DXGI,1,0,0,0.600,16.69,16.69,0.41,2.03,16.53,8.12,12.35,16.53,600000000,0,Hardware: Independent Flip\n";

        var launcher = new FakePresentMonLauncher(csvContent: csv);
        var provider = new PresentMonProvider(launcher: launcher);
        await provider.InitAsync();

        var epoch = Stopwatch.GetTimestamp();
        await provider.StartAsync(epoch);

        var samples = new List<FrameTimeSample>();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await foreach (var evt in provider.Events.WithCancellation(cts.Token))
        {
            if (evt is FrameTimeSample sample)
                samples.Add(sample);
        }

        await provider.StopAsync();

        Assert.Equal("GameApp.exe", provider.TrackedApplication);
    }

    [Fact]
    public async Task BorderlessWindowed_NoteSet()
    {
        var csv = StandardHeader + "\n"
            + "Discord.exe,5678,0x2,DXGI,1,0,0,0.500,16.67,16.67,0.30,1.80,16.50,5.20,8.40,16.50,500000000,0,Composed: Flip\n"
            + "Discord.exe,5678,0x2,DXGI,1,0,0,0.517,16.70,16.70,0.32,1.82,16.53,5.25,8.45,16.53,517000000,0,Composed: Flip\n";

        var launcher = new FakePresentMonLauncher(csvContent: csv);
        var provider = new PresentMonProvider(launcher: launcher);
        await provider.InitAsync();

        var epoch = Stopwatch.GetTimestamp();
        await provider.StartAsync(epoch);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await foreach (var _ in provider.Events.WithCancellation(cts.Token)) { }
        await provider.StopAsync();

        Assert.NotNull(provider.BorderlessNote);
        Assert.Contains("composed mode", provider.BorderlessNote);
    }

    [Fact]
    public async Task SubprocessCrash_CrashRecorded()
    {
        var csv = StandardHeader + "\n"
            + "TestGame.exe,1234,0x1,DXGI,1,0,0,0.500,16.67,16.67,0.45,2.10,16.50,8.20,12.30,16.50,500000000,0,Hardware: Independent Flip\n";

        var launcher = new FakePresentMonLauncher(csvContent: csv, exitCode: 1);
        var provider = new PresentMonProvider(launcher: launcher);
        await provider.InitAsync();

        var epoch = Stopwatch.GetTimestamp();
        await provider.StartAsync(epoch);

        var samples = new List<FrameTimeSample>();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await foreach (var evt in provider.Events.WithCancellation(cts.Token))
        {
            if (evt is FrameTimeSample sample)
                samples.Add(sample);
        }

        await provider.StopAsync();

        Assert.Single(samples);
        Assert.True(provider.Crashed);
        Assert.NotNull(provider.CrashNote);
        Assert.Contains("crashed", provider.CrashNote);
    }

    [Fact]
    public async Task ProcessFilter_OnlyMatchingAppEmitted()
    {
        var csv = StandardHeader + "\n"
            + "GameApp.exe,1234,0x1,DXGI,1,0,0,0.500,16.67,16.67,0.45,2.10,16.50,8.20,12.30,16.50,500000000,0,Hardware: Independent Flip\n"
            + "Discord.exe,5678,0x2,DXGI,1,0,0,0.510,33.33,33.33,0.30,1.80,33.00,2.50,3.00,33.00,510000000,0,Composed: Flip\n"
            + "GameApp.exe,1234,0x1,DXGI,1,0,0,0.517,16.70,16.70,0.43,2.05,16.55,8.10,12.50,16.55,517000000,0,Hardware: Independent Flip\n";

        var launcher = new FakePresentMonLauncher(csvContent: csv);
        var provider = new PresentMonProvider(processFilter: "GameApp.exe", launcher: launcher);
        await provider.InitAsync();

        var epoch = Stopwatch.GetTimestamp();
        await provider.StartAsync(epoch);

        var samples = new List<FrameTimeSample>();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await foreach (var evt in provider.Events.WithCancellation(cts.Token))
        {
            if (evt is FrameTimeSample sample)
                samples.Add(sample);
        }

        await provider.StopAsync();

        Assert.Equal(2, samples.Count);
        Assert.All(samples, s => Assert.Equal("GameApp.exe", s.ApplicationName));
    }

    [Fact]
    public async Task DroppedFrame_ParsedCorrectly()
    {
        var csv = StandardHeader + "\n"
            + "TestGame.exe,1234,0x1,DXGI,1,0,0,0.500,16.67,16.67,0.45,2.10,16.50,8.20,12.30,16.50,500000000,1,Hardware: Independent Flip\n";

        var launcher = new FakePresentMonLauncher(csvContent: csv);
        var provider = new PresentMonProvider(launcher: launcher);
        await provider.InitAsync();

        var epoch = Stopwatch.GetTimestamp();
        await provider.StartAsync(epoch);

        var samples = new List<FrameTimeSample>();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await foreach (var evt in provider.Events.WithCancellation(cts.Token))
        {
            if (evt is FrameTimeSample sample)
                samples.Add(sample);
        }

        await provider.StopAsync();

        Assert.Single(samples);
        Assert.True(samples[0].Dropped);
    }

    [Fact]
    public async Task AllowsTearing_ParsedCorrectly()
    {
        var csv = StandardHeader + "\n"
            + "TestGame.exe,1234,0x1,DXGI,0,0,1,0.500,8.33,8.33,0.20,1.50,8.10,4.50,6.80,8.10,500000000,0,Hardware: Independent Flip\n";

        var launcher = new FakePresentMonLauncher(csvContent: csv);
        var provider = new PresentMonProvider(launcher: launcher);
        await provider.InitAsync();

        var epoch = Stopwatch.GetTimestamp();
        await provider.StartAsync(epoch);

        var samples = new List<FrameTimeSample>();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await foreach (var evt in provider.Events.WithCancellation(cts.Token))
        {
            if (evt is FrameTimeSample sample)
                samples.Add(sample);
        }

        await provider.StopAsync();

        Assert.Single(samples);
        Assert.True(samples[0].AllowsTearing);
    }

    [Fact]
    public async Task UnavailableProvider_StartDoesNothing()
    {
        var launcher = new FakePresentMonLauncher(binaryExists: false);
        var provider = new PresentMonProvider(launcher: launcher);
        await provider.InitAsync();

        await provider.StartAsync(Stopwatch.GetTimestamp());
        await provider.StopAsync();

        Assert.Equal(0, provider.TotalFramesParsed);
    }
}
