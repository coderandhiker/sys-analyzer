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

// Detect interactive launch (double-click with no args)
bool isInteractiveLaunch = args.Length == 0 && !Console.IsInputRedirected && !Console.IsOutputRedirected;

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
    if (isInteractiveLaunch) WaitForKeypress();
    return 1;
}

// Default to 5 minutes when launched interactively with no arguments
if (isInteractiveLaunch && !options.Duration.HasValue)
{
    options.Duration = 300;
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

// Auto-elevate: demand admin unless already elevated or --no-elevate
if (!options.NoElevate && !LibreHardwareProvider.IsElevated())
{
    try
    {
        var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        if (exePath == null)
        {
            Console.Error.WriteLine("Error: Could not determine executable path for elevation.");
            return 3;
        }

        // Pass all original args plus --elevate marker so we don't loop
        var elevatedArgs = args.Where(a => a != "--elevate").Append("--elevate").ToArray();

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = string.Join(' ', elevatedArgs),
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = Environment.CurrentDirectory
        };
        Process.Start(psi);
        return 0;
    }
    catch (System.ComponentModel.Win32Exception)
    {
        Console.Error.WriteLine("Warning: Admin access declined. Running with limited sensors (Tier 1).");
        Console.Error.WriteLine("  GPU temps, ETW culprit detection, and some counters will be unavailable.");
        Console.Error.WriteLine("  Use --no-elevate to suppress this prompt.\n");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Warning: Failed to elevate ({ex.Message}). Continuing with Tier 1 sensors only.");
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

// Tier 2: LibreHardwareMonitor (only attempts load when elevated)
var lhmProvider = new LibreHardwareProvider();
providers.Add(lhmProvider);

PresentMonProvider? presentMonProvider = null;
if (config.Capture.PresentmonEnabled)
{
    presentMonProvider = new PresentMonProvider(options.Process);
    providers.Add(presentMonProvider);
}

// ETW provider (gracefully degrades if not elevated)
EtwProvider? etwProvider = null;
if (config.Capture.EtwEnabled)
{
    etwProvider = new EtwProvider();
    if (options.Etl)
    {
        var etlFilename = FilenameGenerator.Generate(
            config.Output.FilenameFormat, config.Output.TimestampFormat, options.Label);
        etwProvider.EtlFilePath = Path.Combine(config.Output.Directory, etlFilename + ".etl");
    }
    providers.Add(etwProvider);
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

// ETW event collection
var etwEvents = new List<EtwEvent>();

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

        // --- Run the full analysis pipeline ---
        List<FrameTimeSample> frameSamplesCopy;
        lock (frameTimeSamples) { frameSamplesCopy = new List<FrameTimeSample>(frameTimeSamples); }
        List<EtwEvent> etwEventsCopy;
        lock (etwEvents) { etwEventsCopy = new List<EtwEvent>(etwEvents); }

        var pipeline = new AnalysisPipeline(config);
        var pipelineResult = pipeline.Run(
            s.Snapshots,
            frameSamplesCopy.Count > 0 ? frameSamplesCopy : null,
            etwEventsCopy.Count > 0 ? etwEventsCopy : null,
            hw,
            sysConfig,
            options.Profile);

        // Map pipeline scores to summary scores
        var scores = new ScoresSummary(
            MapScore(pipelineResult.ScoringResult.Cpu),
            MapScore(pipelineResult.ScoringResult.Memory),
            pipelineResult.ScoringResult.Gpu is not null
                ? MapScore(pipelineResult.ScoringResult.Gpu)
                : null,
            MapScore(pipelineResult.ScoringResult.Disk),
            MapScore(pipelineResult.ScoringResult.Network)
        );

        // Map culprit attribution
        var culpritSummary = CulpritResultMapper.ToSummary(pipelineResult.CulpritResult);

        // Use pipeline frame-time summary (or compute if pipeline didn't have PresentMon data)
        var frameTimeSummary = pipelineResult.FrameTimeSummary;
        if (frameTimeSummary == null && presentMonProvider != null && frameSamplesCopy.Count > 0)
        {
            var notes = new List<string>();
            if (presentMonProvider.CrashNote != null)
                notes.Add(presentMonProvider.CrashNote);
            if (presentMonProvider.BorderlessNote != null)
                notes.Add(presentMonProvider.BorderlessNote);

            var stutterMultiplier = config.Thresholds.FrameTime.TryGetValue("stutter_spike_multiplier", out var sm) ? sm : 2.0;
            var cpuBoundRatio = config.Thresholds.FrameTime.TryGetValue("cpu_bound_ratio", out var cbr) ? cbr : 1.5;

            frameTimeSummary = FrameTimeAggregator.Compute(
                frameSamplesCopy, presentMonProvider.TrackedApplication,
                stutterMultiplier, cpuBoundRatio,
                notes.Count > 0 ? notes : null);
        }

        // Baseline comparison (needs summary built first, so defer)
        BaselineComparisonSummary? baselineComparison = null;

        // Snapshot top memory consumers
        var topMemoryProcesses = CaptureTopMemoryProcesses();

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
            CulpritAttribution: culpritSummary,
            Recommendations: pipelineResult.Recommendations,
            BaselineComparison: null,
            SelfOverhead: overhead,
            TimeSeries: new TimeSeriesMetadata(s.SampleCount, duration, 1),
            TopMemoryProcesses: topMemoryProcesses
        );

        // Now run baseline comparison with the built summary
        if (options.Compare != null)
        {
            try
            {
                var baselineJson = await File.ReadAllTextAsync(options.Compare);
                var baselineSummary = JsonReportGenerator.Deserialize(baselineJson);
                if (baselineSummary != null)
                {
                    var comparator = new BaselineComparator();
                    var baselineResult = comparator.Compare(summary, baselineSummary);
                    if (baselineResult != null)
                    {
                        baselineComparison = new BaselineComparisonSummary(
                            baselineResult.BaselineId,
                            baselineResult.FingerprintMatch,
                            baselineResult.MetricDeltas.Select(d =>
                                new DeltaEntry(d.Metric, d.BaselineValue, d.CurrentValue, d.Change))
                            .ToList());
                        // Rebuild summary with baseline
                        summary = summary with { BaselineComparison = baselineComparison };
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: Baseline comparison failed: {ex.Message}");
            }
        }

        // Write JSON report
        var outputDir = config.Output.Directory;
        var reportPath = await JsonReportGenerator.WriteToFileAsync(
            summary, outputDir, config.Output.FilenameFormat,
            config.Output.TimestampFormat, options.Label);
        Console.WriteLine($"  JSON: {reportPath}");

        // Write HTML report
        try
        {
            var htmlGenerator = new HtmlReportGenerator();
            var htmlPath = await htmlGenerator.GenerateAsync(
                summary, s.Snapshots, frameSamplesCopy,
                outputDir, config.Output.FilenameFormat,
                config.Output.TimestampFormat, options.Label);
            Console.WriteLine($"  HTML: {htmlPath}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: HTML report failed: {ex.Message}");
        }

        // CSV export (only when --csv flag is passed)
        if (options.Csv)
        {
            try
            {
                var filenameBase = FilenameGenerator.Generate(
                    config.Output.FilenameFormat, config.Output.TimestampFormat, options.Label);

                var csvPath = await CsvExporter.ExportTimeSeriesAsync(
                    s.Snapshots, outputDir, filenameBase);
                Console.WriteLine($"  CSV:  {csvPath}");

                if (frameSamplesCopy.Count > 0)
                {
                    var pmCsvPath = await CsvExporter.ExportPresentMonAsync(
                        frameSamplesCopy, outputDir, filenameBase);
                    Console.WriteLine($"  CSV:  {pmCsvPath}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: CSV export failed: {ex.Message}");
            }
        }

        // ETL export notification
        if (options.Etl && etwProvider?.EtlFilePath != null)
        {
            Console.WriteLine($"  ETL:  {etwProvider.EtlFilePath}");
        }

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
        string symbol;
        string suffix = "";
        if (name == "LibreHardwareMonitor")
        {
            symbol = health.Status switch
            {
                ProviderStatus.Active => "\u2705",  // ✅
                ProviderStatus.Unavailable => "\u26A0\uFE0F",  // ⚠
                _ => "\u274C"  // ❌
            };
            suffix = health.Status switch
            {
                ProviderStatus.Active => $" (Tier 2 \u2014 admin)",
                ProviderStatus.Unavailable => " \u2192 Accept UAC prompt for Tier 2",
                ProviderStatus.Failed => " \u2192 Tier 2 sensors unavailable",
                _ => ""
            };
        }
        else
        {
            symbol = health.Status switch
            {
                ProviderStatus.Active => "[OK]",
                ProviderStatus.Degraded => "[!!]",
                _ => "[XX]"
            };
        }
        Console.WriteLine($"  {symbol} {name}: {health.MetricsAvailable}/{health.MetricsExpected} metrics{suffix}");
        if (health.DegradationReason != null)
            Console.WriteLine($"       {health.DegradationReason}");
    }

    // Show overall tier
    Console.WriteLine($"  Tier: {session.HealthMatrix.OverallTier}");

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

    // Collect ETW events in background
    Task? etwCollectionTask = null;
    if (etwProvider != null && etwProvider.Health.Status != ProviderStatus.Unavailable
        && etwProvider.Health.Status != ProviderStatus.Failed)
    {
        etwCollectionTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var evt in etwProvider.Events.WithCancellation(cts.Token))
                {
                    if (evt is EtwEvent etwEvt)
                    {
                        lock (etwEvents) { etwEvents.Add(etwEvt); }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch { /* ETW collection failure doesn't crash capture */ }
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

    // Wait for ETW collection to finish
    if (etwCollectionTask != null)
    {
        try { await etwCollectionTask.WaitAsync(TimeSpan.FromSeconds(5)); }
        catch { /* timeout ok */ }
    }

    await session.FinishAsync();

    // Quick summary box
    Console.WriteLine();
    Console.WriteLine($"  Capture complete. {session.SampleCount} samples collected.");
    int frameCount;
    lock (frameTimeSamples) { frameCount = frameTimeSamples.Count; }
    if (frameCount > 0)
        Console.WriteLine($"  Frames: {frameCount}");
    int etwCount;
    lock (etwEvents) { etwCount = etwEvents.Count; }
    if (etwCount > 0)
        Console.WriteLine($"  ETW events: {etwCount}");
    Console.WriteLine();

    if (isInteractiveLaunch) WaitForKeypress();
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    if (isInteractiveLaunch) WaitForKeypress();
    return 3;
}

static void WaitForKeypress()
{
    Console.WriteLine("Press any key to exit...");
    try { Console.ReadKey(intercept: true); } catch { }
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

static Dictionary<string, string> GetMetricDescriptions() => new()
{
    // CPU
    ["avg_load"] = "How busy your CPU was on average (lower is better)",
    ["p95_load"] = "How busy your CPU was during intense moments",
    ["thermal_throttle"] = "Time your CPU was too hot and had to slow down",
    ["single_core_saturation"] = "Time a single CPU core was maxed out (common in older games)",
    ["dpc_time"] = "Time spent on driver overhead — high values mean a driver is hogging the CPU",
    ["clock_drop"] = "How much your CPU slowed down from its top speed (overheating or power limits)",
    // Memory
    ["avg_utilization"] = "How full your RAM was on average — above 85% means you're running low",
    ["page_fault_rate"] = "How often programs asked for memory that wasn't ready — very high = too many programs open",
    ["hard_fault_rate"] = "How often Windows had to read from disk because RAM was full — causes stutters",
    ["commit_ratio"] = "Total memory promised to programs vs. what's available — above 90% means you're near the limit",
    ["low_available"] = "Time your system had almost no free RAM left",
    // GPU
    ["vram_utilization"] = "How full your graphics card's memory (VRAM) was",
    ["vram_overflow"] = "Your game tried to use more VRAM than your GPU has — causes major stutters",
    ["power_throttle"] = "Your GPU hit its power limit and had to slow down",
    // Disk
    ["avg_queue_length"] = "How backed up your disk was with read/write requests",
    ["avg_latency"] = "How long your disk took to respond — higher means slower loading",
    ["active_time"] = "How much of the time your disk was busy",
    ["is_hdd"] = "You're using a hard drive instead of an SSD — HDDs are much slower for gaming",
    // Network
    ["retransmit_rate"] = "How often data had to be resent over your network — high means packet loss or bad connection",
    ["bandwidth_ceiling"] = "Your internet connection is close to being maxed out",
};

static CategoryScore MapScore(SubsystemScore sub)
{
    var descriptions = GetMetricDescriptions();
    var components = sub.ComponentDetails?.Select(c =>
        new ScoreComponent(
            c.Name,
            c.RawValue,
            Math.Round(c.NormalizedValue, 1),
            c.Weight,
            descriptions.GetValueOrDefault(c.Name, c.Name.Replace('_', ' '))
        )).ToList();

    return new CategoryScore(
        sub.Score ?? 0,
        sub.Classification,
        sub.AvailableMetrics,
        sub.TotalMetrics,
        components,
        sub.Missing.Count > 0 ? sub.Missing.ToList() : null
    );
}

static List<MemoryProcessEntry> CaptureTopMemoryProcesses()
{
    try
    {
        long totalPhysicalBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        double totalMb = totalPhysicalBytes / (1024.0 * 1024.0);

        var knownDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["explorer"] = "Windows Shell — Desktop, Taskbar, File Explorer",
            ["dwm"] = "Desktop Window Manager — composits all windows",
            ["SearchIndexer"] = "Windows Search indexing service",
            ["MsMpEng"] = "Windows Defender antimalware engine",
            ["svchost"] = "Windows service host process",
            ["csrss"] = "Windows Client/Server Runtime — core system process",
            ["lsass"] = "Local Security Authority — authentication",
            ["RuntimeBroker"] = "UWP app permission broker",
            ["ShellExperienceHost"] = "Windows Shell Experience (Start Menu, Action Center)",
            ["SecurityHealthService"] = "Windows Security service",
            ["OneDrive"] = "Microsoft OneDrive sync client",
            ["Teams"] = "Microsoft Teams",
            ["chrome"] = "Google Chrome browser",
            ["msedge"] = "Microsoft Edge browser",
            ["firefox"] = "Mozilla Firefox browser",
            ["discord"] = "Discord voice/chat client",
            ["Spotify"] = "Spotify music player",
            ["steam"] = "Steam game platform client",
            ["steamwebhelper"] = "Steam embedded browser (Chromium)",
            ["EpicGamesLauncher"] = "Epic Games Store launcher",
            ["NahimicService"] = "Nahimic audio processing service",
            ["Corsair.Service"] = "Corsair iCUE peripheral management",
            ["iCUE"] = "Corsair iCUE RGB/peripheral software",
            ["NZXT CAM"] = "NZXT CAM hardware monitoring",
            ["RazerCentral"] = "Razer Synapse peripheral management",
            ["Wallpaper32"] = "Wallpaper Engine (animated desktop)",
            ["WallpaperService32"] = "Wallpaper Engine service",
            ["obs64"] = "OBS Studio — streaming/recording",
            ["Code"] = "Visual Studio Code",
            ["devenv"] = "Visual Studio IDE",
            ["WindowsTerminal"] = "Windows Terminal",
            ["powershell"] = "PowerShell",
        };

        var processes = System.Diagnostics.Process.GetProcesses();
        var grouped = new Dictionary<string, (long ws, long priv, string? desc)>(StringComparer.OrdinalIgnoreCase);

        foreach (var proc in processes)
        {
            try
            {
                string name = proc.ProcessName;
                long ws = proc.WorkingSet64;
                long priv = proc.PrivateMemorySize64;

                if (grouped.TryGetValue(name, out var existing))
                {
                    grouped[name] = (existing.ws + ws, existing.priv + priv, existing.desc);
                }
                else
                {
                    knownDescriptions.TryGetValue(name, out string? desc);
                    grouped[name] = (ws, priv, desc);
                }
            }
            catch { /* access denied for some system processes — skip */ }
        }

        foreach (var proc in processes)
        {
            try { proc.Dispose(); } catch { }
        }

        return grouped
            .OrderByDescending(kv => kv.Value.ws)
            .Take(20)
            .Select(kv => new MemoryProcessEntry(
                kv.Key,
                kv.Value.ws,
                kv.Value.priv,
                Math.Round(kv.Value.ws / (double)totalPhysicalBytes * 100.0, 1),
                kv.Value.desc))
            .ToList();
    }
    catch
    {
        return [];
    }
}
