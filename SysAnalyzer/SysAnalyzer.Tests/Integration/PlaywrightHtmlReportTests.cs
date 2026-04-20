using Microsoft.Playwright;
using SysAnalyzer.Analysis.Models;
using SysAnalyzer.Capture;
using SysAnalyzer.Report;

namespace SysAnalyzer.Tests.Integration;

/// <summary>
/// Playwright-based browser tests verifying the HTML report renders correctly.
/// Tests cover: no JS errors, charts render, scores display, conditional sections,
/// cross-browser compatibility (Chromium, Firefox, WebKit).
/// </summary>
public class PlaywrightHtmlReportTests : IAsyncLifetime
{
    private IPlaywright _playwright = null!;
    private string _htmlFilePath = null!;
    private string _tempDir = null!;

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _tempDir = Path.Combine(Path.GetTempPath(), $"sysanalyzer-pw-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        // Generate a full-featured HTML report for browser testing
        var summary = CreateFullSummary();
        var snapshots = CreateTestSnapshots(60);
        var frameSamples = CreateTestFrameSamples(200);

        var generator = new HtmlReportGenerator();
        _htmlFilePath = await generator.GenerateAsync(
            summary, snapshots, frameSamples, _tempDir,
            "playwright-test-{timestamp}", "yyyyMMdd", "browser-test");
    }

    public async Task DisposeAsync()
    {
        _playwright.Dispose();
        await Task.Delay(100); // let handles release
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); }
        catch { /* best effort */ }
    }

    // ──────────────────────────────────────────────────────────────────
    // CHROMIUM TESTS
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Chromium_NoJavaScriptErrors()
    {
        await using var browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
        var errors = await CollectJsErrors(browser);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Chromium_ScoreCardsRender()
    {
        await using var browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
        await AssertScoreCardsRender(browser);
    }

    [Fact]
    public async Task Chromium_ChartsRender()
    {
        await using var browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
        await AssertChartsRender(browser);
    }

    [Fact]
    public async Task Chromium_RecommendationsRender()
    {
        await using var browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
        await AssertRecommendationsRender(browser);
    }

    [Fact]
    public async Task Chromium_CulpritSectionRender()
    {
        await using var browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
        await AssertCulpritSectionRender(browser);
    }

    [Fact]
    public async Task Chromium_ConditionalSections_FrameTimeVisible()
    {
        await using var browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
        await AssertFrameTimeSectionVisible(browser);
    }

    [Fact]
    public async Task Chromium_BaselineComparisonRenders()
    {
        await using var browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
        await AssertBaselineRenders(browser);
    }

    [Fact]
    public async Task Chromium_NoNetworkRequests()
    {
        await using var browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
        await AssertNoNetworkRequests(browser);
    }

    [Fact]
    public async Task Chromium_HardwareInventoryRenders()
    {
        await using var browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
        await AssertHardwareInventoryRenders(browser);
    }

    [Fact]
    public async Task Chromium_SystemConfigRenders()
    {
        await using var browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
        await AssertSystemConfigRenders(browser);
    }

    // ──────────────────────────────────────────────────────────────────
    // FIREFOX TESTS
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Firefox_NoJavaScriptErrors()
    {
        await using var browser = await _playwright.Firefox.LaunchAsync(new() { Headless = true });
        var errors = await CollectJsErrors(browser);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Firefox_ScoreCardsRender()
    {
        await using var browser = await _playwright.Firefox.LaunchAsync(new() { Headless = true });
        await AssertScoreCardsRender(browser);
    }

    [Fact]
    public async Task Firefox_ChartsRender()
    {
        await using var browser = await _playwright.Firefox.LaunchAsync(new() { Headless = true });
        await AssertChartsRender(browser);
    }

    [Fact]
    public async Task Firefox_NoNetworkRequests()
    {
        await using var browser = await _playwright.Firefox.LaunchAsync(new() { Headless = true });
        await AssertNoNetworkRequests(browser);
    }

    // ──────────────────────────────────────────────────────────────────
    // WEBKIT (Safari) TESTS
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task WebKit_NoJavaScriptErrors()
    {
        await using var browser = await _playwright.Webkit.LaunchAsync(new() { Headless = true });
        var errors = await CollectJsErrors(browser);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task WebKit_ScoreCardsRender()
    {
        await using var browser = await _playwright.Webkit.LaunchAsync(new() { Headless = true });
        await AssertScoreCardsRender(browser);
    }

    [Fact]
    public async Task WebKit_ChartsRender()
    {
        await using var browser = await _playwright.Webkit.LaunchAsync(new() { Headless = true });
        await AssertChartsRender(browser);
    }

    [Fact]
    public async Task WebKit_NoNetworkRequests()
    {
        await using var browser = await _playwright.Webkit.LaunchAsync(new() { Headless = true });
        await AssertNoNetworkRequests(browser);
    }

    // ──────────────────────────────────────────────────────────────────
    // CONDITIONAL SECTION TESTS (Tier 1 / no frame timing)
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Chromium_Tier1Report_HidesFrameTimingSection()
    {
        // Generate a Tier 1 report (no frame timing, no culprits)
        var tier1Html = BuildTier1Html();
        var tier1Path = Path.Combine(_tempDir, "tier1-test.html");
        await File.WriteAllTextAsync(tier1Path, tier1Html);

        await using var browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync();
        await page.GotoAsync($"file:///{tier1Path.Replace('\\', '/')}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Frame timing section should be hidden
        var frameSection = page.Locator("#frame-timing-section");
        var isHidden = await frameSection.EvaluateAsync<bool>("el => el.classList.contains('section-hidden')");
        Assert.True(isHidden, "Frame timing section should be hidden for Tier 1 report");

        // Culprit section should be hidden
        var culpritSection = page.Locator("#culprit-section");
        var culpritHidden = await culpritSection.EvaluateAsync<bool>("el => el.classList.contains('section-hidden')");
        Assert.True(culpritHidden, "Culprit section should be hidden when no attribution data");

        // Baseline section should be hidden
        var baselineSection = page.Locator("#baseline-section");
        var baselineHidden = await baselineSection.EvaluateAsync<bool>("el => el.classList.contains('section-hidden')");
        Assert.True(baselineHidden, "Baseline section should be hidden when no comparison data");
    }

    [Fact]
    public async Task Chromium_ScoreCardColors_MatchSeverity()
    {
        await using var browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync();
        await page.GotoAsync($"file:///{_htmlFilePath.Replace('\\', '/')}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // CPU score is 45 (Moderate) → should have warning color
        var cpuCard = page.Locator(".score-card").First;
        var cpuProgressBar = cpuCard.Locator(".progress-bar");
        var cpuBarClass = await cpuProgressBar.GetAttributeAsync("class") ?? "";
        Assert.Contains("bg-warning", cpuBarClass);

        // GPU score is 82 (Stressed) → should have danger color
        // Find the GPU card (third score card since we have CPU, Memory, GPU)
        var gpuCard = page.Locator(".score-card").Nth(2);
        var gpuProgressBar = gpuCard.Locator(".progress-bar");
        var gpuBarClass = await gpuProgressBar.GetAttributeAsync("class") ?? "";
        Assert.Contains("bg-danger", gpuBarClass);
    }

    // ──────────────────────────────────────────────────────────────────
    // HELPER METHODS
    // ──────────────────────────────────────────────────────────────────

    private async Task<List<string>> CollectJsErrors(IBrowser browser)
    {
        var errors = new List<string>();
        var page = await browser.NewPageAsync();

        // PageError captures actual uncaught JS exceptions (what we care about)
        page.PageError += (_, error) => errors.Add(error);

        // Console "error" events include resource-loading noise (fonts, favicons)
        // that are expected when opening a self-contained HTML via file://.
        // Only capture console errors that indicate real JS problems.
        page.Console += (_, msg) =>
        {
            if (msg.Type != "error") return;
            var text = msg.Text;
            // Filter out resource-loading noise common in file:// context
            if (text.Contains("net::ERR_FILE_NOT_FOUND")) return;
            if (text.Contains("Failed to load resource")) return;
            if (text.Contains("downloadable font")) return;
            if (text.Contains("Not allowed to load local resource")) return;
            if (text.Contains("favicon")) return;
            errors.Add(text);
        };

        await page.GotoAsync($"file:///{_htmlFilePath.Replace('\\', '/')}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        // Give charts time to render
        await page.WaitForTimeoutAsync(2000);
        return errors;
    }

    private async Task AssertScoreCardsRender(IBrowser browser)
    {
        var page = await browser.NewPageAsync();
        await page.GotoAsync($"file:///{_htmlFilePath.Replace('\\', '/')}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Score cards should be rendered with actual values
        var scoreCards = page.Locator(".score-card");
        var count = await scoreCards.CountAsync();
        Assert.InRange(count, 4, 5); // CPU, Memory, GPU (optional), Disk, Network

        // Each score card should have a progress bar
        var progressBars = page.Locator(".score-card .progress-bar");
        var barCount = await progressBars.CountAsync();
        Assert.Equal(count, barCount);

        // Score values should be visible (display-6 class)
        var scoreValues = page.Locator(".score-card .display-6");
        var firstScore = await scoreValues.First.TextContentAsync();
        Assert.False(string.IsNullOrWhiteSpace(firstScore), "Score value should not be empty");
        Assert.True(int.TryParse(firstScore!.Trim(), out int scoreVal), "Score should be a number");
        Assert.InRange(scoreVal, 0, 100);
    }

    private async Task AssertChartsRender(IBrowser browser)
    {
        var page = await browser.NewPageAsync();
        await page.GotoAsync($"file:///{_htmlFilePath.Replace('\\', '/')}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        // Wait for ApexCharts to render
        await page.WaitForTimeoutAsync(3000);

        // ApexCharts renders SVG elements inside chart containers
        var cpuChartSvg = page.Locator("#chart-cpu svg");
        var cpuCount = await cpuChartSvg.CountAsync();
        Assert.True(cpuCount > 0, "CPU chart should have SVG rendered by ApexCharts");

        var memChartSvg = page.Locator("#chart-memory svg");
        var memCount = await memChartSvg.CountAsync();
        Assert.True(memCount > 0, "Memory chart should have SVG rendered by ApexCharts");

        // Frame time chart should be present since we have frame data
        var ftChartSvg = page.Locator("#chart-frametime svg");
        var ftCount = await ftChartSvg.CountAsync();
        Assert.True(ftCount > 0, "Frame time chart should have SVG rendered by ApexCharts");

        // Histogram should be present
        var histChartSvg = page.Locator("#chart-histogram svg");
        var histCount = await histChartSvg.CountAsync();
        Assert.True(histCount > 0, "Histogram chart should have SVG rendered by ApexCharts");
    }

    private async Task AssertRecommendationsRender(IBrowser browser)
    {
        var page = await browser.NewPageAsync();
        await page.GotoAsync($"file:///{_htmlFilePath.Replace('\\', '/')}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Recommendations section should be visible
        var recSection = page.Locator("#recommendations-section");
        var isHidden = await recSection.EvaluateAsync<bool>("el => el.classList.contains('section-hidden')");
        Assert.False(isHidden, "Recommendations section should be visible when recommendations exist");

        // Should have recommendation alerts
        var alerts = page.Locator("#recommendations-body .alert");
        var alertCount = await alerts.CountAsync();
        Assert.True(alertCount > 0, "Should have at least one recommendation alert");

        // Check the title text is rendered
        var bodyText = await page.Locator("#recommendations-body").TextContentAsync();
        Assert.Contains("GPU-Bound Rendering", bodyText);
    }

    private async Task AssertCulpritSectionRender(IBrowser browser)
    {
        var page = await browser.NewPageAsync();
        await page.GotoAsync($"file:///{_htmlFilePath.Replace('\\', '/')}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Culprit section should be visible
        var section = page.Locator("#culprit-section");
        var isHidden = await section.EvaluateAsync<bool>("el => el.classList.contains('section-hidden')");
        Assert.False(isHidden, "Culprit section should be visible");

        // Should have culprit process names
        var bodyText = await page.Locator("#culprit-body").TextContentAsync();
        Assert.Contains("dwm.exe", bodyText);
        Assert.Contains("SearchIndexer.exe", bodyText);
    }

    private async Task AssertFrameTimeSectionVisible(IBrowser browser)
    {
        var page = await browser.NewPageAsync();
        await page.GotoAsync($"file:///{_htmlFilePath.Replace('\\', '/')}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Frame timing section should be visible
        var section = page.Locator("#frame-timing-section");
        var isHidden = await section.EvaluateAsync<bool>("el => el.classList.contains('section-hidden')");
        Assert.False(isHidden, "Frame timing section should be visible when frame data exists");

        // Should show FPS values
        var metricsText = await page.Locator("#frame-timing-metrics").TextContentAsync();
        Assert.Contains("72.5", metricsText); // avgFps
    }

    private async Task AssertBaselineRenders(IBrowser browser)
    {
        var page = await browser.NewPageAsync();
        await page.GotoAsync($"file:///{_htmlFilePath.Replace('\\', '/')}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Baseline section should be visible
        var section = page.Locator("#baseline-section");
        var isHidden = await section.EvaluateAsync<bool>("el => el.classList.contains('section-hidden')");
        Assert.False(isHidden, "Baseline section should be visible");

        // Should show delta values
        var tableText = await page.Locator("#baseline-tbody").TextContentAsync();
        Assert.Contains("cpu.avg_load", tableText);
    }

    private async Task AssertNoNetworkRequests(IBrowser browser)
    {
        var networkRequests = new List<string>();
        var page = await browser.NewPageAsync();
        page.Request += (_, request) =>
        {
            var url = request.Url;
            // Filter out the file:// request for the HTML itself
            if (!url.StartsWith("file://"))
                networkRequests.Add(url);
        };

        await page.GotoAsync($"file:///{_htmlFilePath.Replace('\\', '/')}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.WaitForTimeoutAsync(2000);

        Assert.Empty(networkRequests);
    }

    private async Task AssertHardwareInventoryRenders(IBrowser browser)
    {
        var page = await browser.NewPageAsync();
        await page.GotoAsync($"file:///{_htmlFilePath.Replace('\\', '/')}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var hwText = await page.Locator("#hardware-body").TextContentAsync();
        Assert.Contains("AMD Ryzen 7 5800X", hwText);
        Assert.Contains("NVIDIA RTX 3080", hwText);
        Assert.Contains("32 GB", hwText);
    }

    private async Task AssertSystemConfigRenders(IBrowser browser)
    {
        var page = await browser.NewPageAsync();
        await page.GotoAsync($"file:///{_htmlFilePath.Replace('\\', '/')}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var configText = await page.Locator("#sysconfig-list").TextContentAsync();
        Assert.Contains("High performance", configText);
        Assert.Contains("Windows Defender", configText);
    }

    // ──────────────────────────────────────────────────────────────────
    // DATA FIXTURES
    // ──────────────────────────────────────────────────────────────────

    private string BuildTier1Html()
    {
        var summary = CreateTier1Summary();
        var snapshots = CreateTestSnapshots(10, hasGpu: false);
        var generator = new HtmlReportGenerator();
        return generator.BuildHtml(summary, snapshots, null);
    }

    private static AnalysisSummary CreateFullSummary()
    {
        var metadata = new AnalysisMetadata(
            "1.0.0", DateTime.UtcNow, 120.5, "browser-test", "gaming", "Tier2", "cap-pw-001");

        var fingerprint = new MachineFingerprint(
            "AMD Ryzen 7 5800X", "NVIDIA RTX 3080", 32, "2x16GB DDR4-3600",
            "22631.3880", "2560x1440@144Hz", "nvme-sn850x", "560", "ASUS ROG STRIX");

        var sensorHealth = new SensorHealthSummary("Tier2", new List<ProviderHealthEntry>
        {
            new("PerformanceCounters", "Active", null, 15, 15, 0),
            new("LibreHardwareMonitor", "Active", null, 7, 7, 0),
            new("ETW", "Degraded", "Lost 42 events", 3, 4, 42),
            new("PresentMon", "Active", null, 8, 8, 0)
        });

        var hwInventory = new HardwareInventorySummary(
            "AMD Ryzen 7 5800X", 8, 16, 3800, 4700, 33554432,
            new List<RamStickSummary>
            {
                new("DIMM_A1", 17179869184, 3600, "DDR4"),
                new("DIMM_B1", 17179869184, 3600, "DDR4")
            },
            32, 4, 2, "NVIDIA RTX 3080", 10240, "560.94",
            new List<DiskDriveSummary> { new("WD_BLACK SN850X 1TB", "NVMe", 1000204886016) },
            "ASUS ROG STRIX B550-F", "2803", "2024-01-15", "22631.3880", "Windows 11 23H2",
            new List<NetworkAdapterSummary> { new("Intel I225-V", 2500000000) },
            "2560x1440", 144);

        var sysConfig = new SystemConfigurationSummary(
            "High performance", true, true, false, false, true,
            450, 1200, "System managed", 12, "Windows Defender");

        var scores = new ScoresSummary(
            new CategoryScore(45, "Moderate", 6, 6),
            new CategoryScore(30, "Healthy", 5, 5),
            new CategoryScore(82, "Stressed", 6, 6),
            new CategoryScore(15, "Healthy", 4, 4),
            new CategoryScore(5, "Healthy", 3, 3));

        var frameTime = new FrameTimeSummary(
            true, "Cyberpunk2077.exe", 72.5, 30.0, 13.8, 18.2, 28.5, 45.1,
            0.3, 15.2, 78.5, "Hardware: Independent Flip", true, 7,
            new List<string> { "GPU-bound rendering detected" });

        var culprits = new CulpritAttribution(
            new List<ProcessEntry>
            {
                new("dwm.exe", 12.5, "Desktop Window Manager", "N/A", 0.3),
                new("Discord.exe", 8.3, "Discord overlay", "Close overlay", 0.15)
            },
            new List<DpcDriverEntry> { new("ndis.sys", 2.1, "Network driver") },
            new List<DiskProcessEntry>
            {
                new("SearchIndexer.exe", 35.0, "Windows Search Indexer", "Disable during gaming", 0.4)
            });

        var recommendations = new List<RecommendationEntry>
        {
            new("ft_gpu_bound", "GPU-Bound Rendering",
                "78.5% of frames were GPU-bound. Consider lowering graphics quality.",
                "warning", "frametime", "high", 8,
                new List<string> { "gpu_bound_pct=78.5", "avg_gpu_load=92.1" }),
            new("sw_search_indexer", "Windows Search Indexer Active",
                "SearchIndexer.exe consumed 35% of disk I/O.",
                "info", "config", "medium", 4,
                new List<string> { "disk_io_top_process=SearchIndexer.exe" }),
            new("cfg_disable_wsearch", "Consider Disabling Windows Search",
                "Windows Search was running during capture.",
                "info", "config", "low", 2,
                new List<string> { "wsearch_running=true" })
        };

        var baseline = new BaselineComparisonSummary("cap-baseline-001", true,
            new List<DeltaEntry>
            {
                new("cpu.avg_load", 52.3, 45.0, -7.3),
                new("gpu.avg_load", 88.0, 92.1, 4.1),
                new("memory.utilization", 60.0, 58.5, -1.5)
            });

        var selfOverhead = new SelfOverhead(0.8, 67108864, 3, 1.2, 0);
        var timeSeries = new TimeSeriesMetadata(120, 120.5, 1);

        return new AnalysisSummary(
            metadata, fingerprint, sensorHealth, hwInventory, sysConfig,
            scores, frameTime, culprits, recommendations, baseline,
            selfOverhead, timeSeries);
    }

    private static AnalysisSummary CreateTier1Summary()
    {
        var metadata = new AnalysisMetadata(
            "1.0.0", DateTime.UtcNow, 60.0, "tier1-test", "gaming", "Tier1", "cap-tier1-001");

        var fingerprint = new MachineFingerprint(
            "AMD Ryzen 7 5800X", "NVIDIA RTX 3080", 32, "2x16GB DDR4-3600",
            "22631.3880", "2560x1440@144Hz", "nvme-sn850x", "560", "ASUS ROG STRIX");

        var sensorHealth = new SensorHealthSummary("Tier1", new List<ProviderHealthEntry>
        {
            new("PerformanceCounters", "Active", null, 15, 15, 0),
            new("LibreHardwareMonitor", "Unavailable", "Not elevated", 0, 7, 0)
        });

        var hwInventory = new HardwareInventorySummary(
            "AMD Ryzen 7 5800X", 8, 16, 3800, 4700, 33554432,
            new List<RamStickSummary> { new("DIMM_A1", 17179869184, 3600, "DDR4") },
            32, 4, 2, null, null, null,
            new List<DiskDriveSummary> { new("WD_BLACK SN850X", "NVMe", 1000204886016) },
            "ASUS ROG STRIX", "2803", "2024-01-15", "22631.3880", "Windows 11 23H2",
            new List<NetworkAdapterSummary> { new("Intel I225-V", 2500000000) },
            "2560x1440", 144);

        var sysConfig = new SystemConfigurationSummary(
            "Balanced", true, false, true, true, true,
            200, 800, "System managed", 18, "Windows Defender");

        var scores = new ScoresSummary(
            new CategoryScore(35, "Moderate", 4, 6),
            new CategoryScore(25, "Healthy", 4, 5),
            null, // no GPU data
            new CategoryScore(20, "Healthy", 3, 4),
            new CategoryScore(5, "Healthy", 2, 3));

        var selfOverhead = new SelfOverhead(0.5, 52000000, 2, 0.8, 0);
        var timeSeries = new TimeSeriesMetadata(60, 60.0, 1);

        return new AnalysisSummary(
            metadata, fingerprint, sensorHealth, hwInventory, sysConfig,
            scores, null, null, new List<RecommendationEntry>(), null,
            selfOverhead, timeSeries);
    }

    private static List<SensorSnapshot> CreateTestSnapshots(int count, bool hasGpu = true)
    {
        var snapshots = new List<SensorSnapshot>();
        for (int i = 0; i < count; i++)
        {
            snapshots.Add(new SensorSnapshot(
                Timestamp: new QpcTimestamp((long)(i * QpcTimestamp.Frequency)),
                TotalCpuPercent: 30 + i * 0.5 + Math.Sin(i * 0.3) * 10,
                PerCoreCpuPercent: new[] { 25.0 + i, 35.0 - i * 0.2, 20.0 + i * 0.8, 40.0 - i * 0.1 },
                ContextSwitchesPerSec: 1000 + i * 10,
                DpcTimePercent: 1.5 + Math.Sin(i * 0.1) * 0.5,
                InterruptsPerSec: 500 + i * 5,
                MemoryUtilizationPercent: 45 + i * 0.3,
                AvailableMemoryMb: 16000 - i * 50,
                PageFaultsPerSec: 200 + i * 5,
                HardFaultsPerSec: 10 + (i % 5 == 0 ? 50 : 0),
                CommittedBytes: 8000000000 + i * 10000000L,
                CommittedBytesInUsePercent: 50 + i * 0.2,
                GpuUtilizationPercent: hasGpu ? 70 + Math.Sin(i * 0.2) * 15 : null,
                GpuMemoryUtilizationPercent: hasGpu ? 60 + i * 0.3 : null,
                GpuMemoryUsedMb: hasGpu ? 6000 + i * 20 : null,
                DiskActiveTimePercent: 20 + Math.Sin(i * 0.5) * 15,
                DiskQueueLength: 1.5 + (i % 10 == 0 ? 5.0 : 0),
                DiskBytesPerSec: 50000000 + i * 500000,
                DiskReadLatencyMs: 2.5 + Math.Sin(i * 0.4) * 1.5,
                DiskWriteLatencyMs: 3.0 + Math.Sin(i * 0.3) * 2.0,
                NetworkBytesPerSec: 1000000 + i * 10000,
                NetworkUtilizationPercent: 5 + i * 0.1,
                TcpRetransmitsPerSec: 0.1,
                CpuTempC: hasGpu ? 65 + i * 0.2 : null,
                CpuClockMhz: hasGpu ? 4200 + Math.Sin(i * 0.1) * 300 : null,
                CpuPowerW: hasGpu ? 85 + Math.Sin(i * 0.15) * 20 : null,
                GpuTempC: hasGpu ? 72 + i * 0.15 : null,
                GpuClockMhz: hasGpu ? 1800 + Math.Sin(i * 0.2) * 200 : null,
                GpuPowerW: hasGpu ? 280 + Math.Sin(i * 0.1) * 40 : null,
                GpuFanRpm: hasGpu ? 1500 + i * 10 : null
            ));
        }
        return snapshots;
    }

    private static List<FrameTimeSample> CreateTestFrameSamples(int count)
    {
        var samples = new List<FrameTimeSample>();
        var random = new Random(42); // deterministic
        for (int i = 0; i < count; i++)
        {
            double frameTime = 13.8 + random.NextDouble() * 5.0;
            // Add occasional stutters
            if (i % 30 == 0) frameTime = 40.0 + random.NextDouble() * 20.0;

            samples.Add(new FrameTimeSample(
                Timestamp: new QpcTimestamp((long)(i * (QpcTimestamp.Frequency / 72.0))),
                ApplicationName: "Cyberpunk2077.exe",
                FrameTimeMs: frameTime,
                CpuBusyMs: frameTime * 0.4,
                GpuBusyMs: frameTime * 0.7,
                Dropped: i % 50 == 0,
                PresentMode: "Hardware: Independent Flip",
                AllowsTearing: true
            ));
        }
        return samples;
    }
}
