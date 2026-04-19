// SysAnalyzer — Windows 11 Bottleneck Analysis Tool

using System.Diagnostics;
using System.Reflection;
using SysAnalyzer.Analysis;
using SysAnalyzer.Analysis.Models;
using SysAnalyzer.Baselines;
using SysAnalyzer.Capture;
using SysAnalyzer.Capture.Providers;
using SysAnalyzer.Cli;
using SysAnalyzer.Config;
using SysAnalyzer.Report;

// Parse CLI arguments
CliOptions options;
try
{
    options = CliParser.Parse(args);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    Console.Error.WriteLine("Run with --help for usage information.");
    return 1;
}

if (options.Help)
{
    Console.WriteLine(CliParser.GetHelpText());
    return 0;
}

if (options.Version)
{
    var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
    Console.WriteLine($"SysAnalyzer {version}");
    return 0;
}

// Handle --elevate: re-launch with admin
if (options.Elevate)
{
    try
    {
        var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        if (exePath == null)
        {
            Console.Error.WriteLine("Error: Could not determine executable path for elevation.");
            return 3;
        }

        // Reconstruct args without --elevate
        var elevatedArgs = args.Where(a => a != "--elevate").ToArray();

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = string.Join(' ', elevatedArgs),
            UseShellExecute = true,
            Verb = "runas"
        };
        Process.Start(psi);
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: Failed to elevate: {ex.Message}");
        return 3;
    }
}

// Load config
AnalyzerConfig config;
try
{
    config = ConfigLoader.Load(options.ConfigPath);
    var validation = new ConfigValidator().Validate(config);
    if (!validation.IsValid)
    {
        foreach (var error in validation.Errors)
            Console.Error.WriteLine($"Config error [{error.Context}]: {error.Message}");
        return 1;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Config error: {ex.Message}");
    return 1;
}

// Apply CLI overrides
if (options.Interval.HasValue)
    config.Capture.PollIntervalMs = options.Interval.Value;
if (options.OutputDir != null)
    config.Output.Directory = options.OutputDir;
if (options.NoPresentmon)
    config.Capture.PresentmonEnabled = false;
if (options.NoEtw)
    config.Capture.EtwEnabled = false;

// Set up capture epoch
QpcTimestamp.SetCaptureEpoch(Stopwatch.GetTimestamp(), DateTime.UtcNow);

// Create providers
var providers = new List<IProvider>();
providers.Add(new PerformanceCounterProvider());
providers.Add(new HardwareInventoryProvider());
providers.Add(new WindowsDeepCheckProvider());

PresentMonProvider? presentMonProvider = null;
if (config.Capture.PresentmonEnabled)
{
    presentMonProvider = new PresentMonProvider(options.Process);
    providers.Add(presentMonProvider);
}

if (providers.Count == 0)
{
    Console.Error.WriteLine("Error: No providers available.");
    return 2;
}

// Set up overhead tracker
var overheadTracker = new SelfOverheadTracker();

// Frame-time sample collection
var frameTimeSamples = new List<FrameTimeSample>();

// Snapshot data holders
SnapshotData? hwSnapshot = null;
SnapshotData? sysConfigSnapshot = null;

// Create capture session
using var session = new CaptureSession(
    providers,
    pollLoopFunc: (s, ct) => new PollLoop(s, config.Capture.PollIntervalMs, overheadTracker).RunAsync(ct),
    analyzeFunc: async s =>
    {
        // Capture snapshots from snapshot providers
        foreach (var sp in s.SnapshotProviders)
        {
            try
            {
                var data = await sp.CaptureAsync();
                if (sp.Name == "HardwareInventory")
                    hwSnapshot = data;
                else if (sp.Name == "WindowsDeepCheck")
                    sysConfigSnapshot = data;
            }
            catch { }
        }
    },
    emitFunc: async s =>
    {
        var hw = hwSnapshot?.Hardware ?? CreateDefaultHardware();
        var sysConfig = sysConfigSnapshot?.Configuration ?? CreateDefaultConfig();

        var fingerprint = new MachineFingerprint(
            CpuModel: hw.CpuModel,
            GpuModel: hw.GpuModel ?? "Unknown",
            TotalRamGb: hw.TotalRamGb,
            RamConfig: $"{hw.RamSticks.Count}x{(hw.RamSticks.Count > 0 ? hw.RamSticks[0].CapacityBytes / (1024L * 1024 * 1024) : 0)}GB",
            OsBuild: hw.OsBuild,
            DisplayConfig: $"{hw.DisplayResolution}@{hw.DisplayRefreshRate}Hz",
            StorageConfigHash: hw.Disks.Count > 0 ? hw.Disks[0].Model.ToLowerInvariant().Replace(' ', '-') : "unknown",
            GpuDriverMajorVersion: hw.GpuDriverVersion?.Split('.')[0] ?? "unknown",
            MotherboardModel: hw.MotherboardModel
        );

        var captureStart = QpcTimestamp.WallClockAnchor;
        var duration = s.Snapshots.Count > 0
            ? s.Snapshots[^1].Timestamp.ToSeconds()
            : 0;

        var overhead = overheadTracker.Finish();

        var healthEntries = s.HealthMatrix.Providers.Select(kv =>
            new ProviderHealthEntry(
                kv.Key, kv.Value.Status.ToString(), kv.Value.DegradationReason,
                kv.Value.MetricsAvailable, kv.Value.MetricsExpected, kv.Value.EventsLost))
            .ToList();

        var hwSummary = new HardwareInventorySummary(
            hw.CpuModel, hw.CpuCores, hw.CpuThreads, hw.CpuBaseClock, hw.CpuMaxBoostClock, hw.CpuCacheBytes,
            hw.RamSticks.Select(r => new RamStickSummary(r.BankLabel, r.CapacityBytes, r.SpeedMhz, r.MemoryType)).ToList(),
            hw.TotalRamGb, hw.TotalMemorySlots, hw.AvailableMemorySlots,
            hw.GpuModel, hw.GpuVramMb, hw.GpuDriverVersion,
            hw.Disks.Select(d => new DiskDriveSummary(d.Model, d.DriveType, d.SizeBytes)).ToList(),
            hw.MotherboardModel, hw.BiosVersion, hw.BiosDate,
            hw.OsBuild, hw.OsVersion,
            hw.NetworkAdapters.Select(n => new NetworkAdapterSummary(n.Name, n.SpeedBps)).ToList(),
            hw.DisplayResolution, hw.DisplayRefreshRate
        );

        var sysConfigSummary = new SystemConfigurationSummary(
            sysConfig.PowerPlan, sysConfig.GameModeEnabled, sysConfig.HagsEnabled,
            sysConfig.GameDvrEnabled, sysConfig.SysMainRunning, sysConfig.WSearchRunning,
            sysConfig.ShaderCacheSizeMb, sysConfig.TempFolderSizeMb, sysConfig.PagefileConfig,
            sysConfig.StartupProgramCount, sysConfig.AvProduct
        );

        // Stub scores for Phase B
        var scores = new ScoresSummary(
            new CategoryScore(0, "Pending", 0, 0),
            new CategoryScore(0, "Pending", 0, 0),
            null,
            new CategoryScore(0, "Pending", 0, 0),
            new CategoryScore(0, "Pending", 0, 0)
        );

        // Compute frame-time summary
        FrameTimeSummary? frameTimeSummary = null;
        if (presentMonProvider != null)
        {
            var notes = new List<string>();
            if (presentMonProvider.CrashNote != null)
                notes.Add(presentMonProvider.CrashNote);
            if (presentMonProvider.BorderlessNote != null)
                notes.Add(presentMonProvider.BorderlessNote);
            if (presentMonProvider.Health.DegradationReason != null
                && presentMonProvider.Health.Status == ProviderStatus.Unavailable)
                notes.Add(presentMonProvider.Health.DegradationReason);

            var stutterMultiplier = config.Thresholds.FrameTime.TryGetValue("stutter_spike_multiplier", out var sm) ? sm : 2.0;
            var cpuBoundRatio = config.Thresholds.FrameTime.TryGetValue("cpu_bound_ratio", out var cbr) ? cbr : 1.5;

            frameTimeSummary = FrameTimeAggregator.Compute(
                frameTimeSamples,
                presentMonProvider.TrackedApplication,
                stutterMultiplier,
                cpuBoundRatio,
                notes.Count > 0 ? notes : null);

            // If provider was unavailable but we have no samples, return a "not available" marker
            if (frameTimeSummary == null && presentMonProvider.Health.Status == ProviderStatus.Unavailable)
            {
                frameTimeSummary = null; // stays null — JSON omits it
            }
        }

        var summary = new AnalysisSummary(
            Metadata: new AnalysisMetadata(
                "1.0.0", captureStart, duration, options.Label, options.Profile,
                s.HealthMatrix.OverallTier.ToString(), $"cap-{captureStart:yyyyMMdd-HHmmss}"),
            Fingerprint: fingerprint,
            SensorHealth: new SensorHealthSummary(s.HealthMatrix.OverallTier.ToString(), healthEntries),
            HardwareInventory: hwSummary,
            SystemConfiguration: sysConfigSummary,
            Scores: scores,
            FrameTime: frameTimeSummary,
            CulpritAttribution: null,
            Recommendations: new List<RecommendationEntry>(),
            BaselineComparison: null,
            SelfOverhead: overhead,
            TimeSeries: new TimeSeriesMetadata(s.SampleCount, duration, 1)
        );

        // Write JSON report
        var outputDir = config.Output.Directory;
        var reportPath = await JsonReportGenerator.WriteToFileAsync(
            summary, outputDir, config.Output.FilenameFormat,
            config.Output.TimestampFormat, options.Label);
        Console.WriteLine($"Report saved to: {reportPath}");

        // Auto-save baseline
        if (config.Baselines.AutoSave)
        {
            try
            {
                var baselineManager = new BaselineManager(config.Baselines.Directory, config.Baselines.MaxStored);
                var json = JsonReportGenerator.Serialize(summary);
                var baselinePath = baselineManager.Save(fingerprint.ComputeHash(), json);
                Console.WriteLine($"Baseline saved to: {baselinePath}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: Baseline save failed: {ex.Message}");
            }
        }
    });

// Set up Ctrl+C handler
var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    if (session.State == CaptureState.Capturing)
    {
        Console.WriteLine("\nStopping capture...");
        session.RequestStop();
    }
    // During Analyzing/Emitting — let it finish
};

// Run
try
{
    Console.WriteLine("Initializing providers...");
    await session.InitAsync();

    // Report health
    foreach (var (name, health) in session.HealthMatrix.Providers)
    {
        var symbol = health.Status switch
        {
            ProviderStatus.Active => "[OK]",
            ProviderStatus.Degraded => "[!!]",
            _ => "[XX]"
        };
        Console.WriteLine($"  {symbol} {name}: {health.MetricsAvailable}/{health.MetricsExpected} metrics");
        if (health.DegradationReason != null)
            Console.WriteLine($"       {health.DegradationReason}");
    }

    // Check if any providers are available
    var activeProviders = session.HealthMatrix.Providers.Values
        .Count(h => h.Status != ProviderStatus.Failed);
    if (activeProviders == 0)
    {
        Console.Error.WriteLine("Error: All providers failed to initialize.");
        return 2;
    }

    // Duration-based or interactive capture
    if (options.Duration.HasValue)
    {
        cts.CancelAfter(TimeSpan.FromSeconds(options.Duration.Value));
        Console.WriteLine($"Capturing for {options.Duration.Value} seconds...");
    }
    else
    {
        Console.WriteLine("Capturing... Press Q/Esc to stop.");
    }

    // Start capture (poll loop runs until cancelled)
    var captureTask = session.StartAsync(cts.Token);

    // Collect frame-time samples from PresentMon in background
    Task? frameCollectionTask = null;
    if (presentMonProvider != null && presentMonProvider.Health.Status != ProviderStatus.Unavailable)
    {
        frameCollectionTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var evt in presentMonProvider.Events.WithCancellation(cts.Token))
                {
                    if (evt is FrameTimeSample sample)
                    {
                        lock (frameTimeSamples) { frameTimeSamples.Add(sample); }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch { /* frame collection failure doesn't crash capture */ }
        });
    }

    // Live display
    LiveDisplay? liveDisplay = null;
    if (!options.NoLive && !Console.IsInputRedirected)
    {
        liveDisplay = new LiveDisplay(session, cts.Token, presentMonProvider, frameTimeSamples);
        liveDisplay.Start();
    }

    // Key detection (Q/Esc) in a separate loop if not --no-live and not duration-only
    if (!options.NoLive && !options.Duration.HasValue && !Console.IsInputRedirected)
    {
        _ = Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(intercept: true);
                    if (key.Key == ConsoleKey.Q || key.Key == ConsoleKey.Escape)
                    {
                        session.RequestStop();
                        break;
                    }
                }
                Thread.Sleep(50);
            }
        });
    }

    await captureTask;
    liveDisplay?.Dispose();

    // Wait for frame collection to finish
    if (frameCollectionTask != null)
    {
        try { await frameCollectionTask.WaitAsync(TimeSpan.FromSeconds(5)); }
        catch { /* timeout ok */ }
    }

    await session.FinishAsync();

    Console.WriteLine($"Capture complete. {session.SampleCount} samples collected.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 3;
}

static HardwareInventory CreateDefaultHardware() => new(
    "Unknown", 0, 0, 0, 0, 0,
    Array.Empty<RamStick>(), 0, 0, 0,
    null, null, null,
    Array.Empty<DiskDrive>(),
    "Unknown", "Unknown", "Unknown",
    "Unknown", "Unknown",
    Array.Empty<NetworkAdapter>(),
    "Unknown", 0);

static SystemConfiguration CreateDefaultConfig() => new(
    "Unknown", false, false, false, false, false, 0, 0, "Unknown", 0, "Unknown");
