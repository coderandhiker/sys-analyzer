using Microsoft.Playwright;

namespace SysAnalyzer.Tests.Integration;

/// <summary>
/// Utility to capture screenshots of an HTML report in multiple browsers.
/// Not a test class — invoked programmatically.
/// </summary>
public class ScreenshotCapture
{
    [Fact]
    [Trait("Category", "Playwright")]
    public async Task CaptureReportScreenshots()
    {
        // Try the latest elevated capture first, fall back to any available
        var candidates = new[]
        {
            @"C:\dev\sys-analyzer\test-captures\sysanalyzer-2026-04-20_10-28-54.html",
            @"C:\dev\sys-analyzer\test-captures\sysanalyzer-2026-04-20_09-20-32.html"
        };
        var htmlPath = candidates.FirstOrDefault(File.Exists);
        if (htmlPath == null)
            return;

        var screenshotDir = @"C:\dev\sys-analyzer\test-dossiers\screenshots";
        Directory.CreateDirectory(screenshotDir);

        using var playwright = await Playwright.CreateAsync();

        // Chromium (Edge/Chrome)
        await CaptureWithBrowser(playwright.Chromium, "chromium", htmlPath, screenshotDir);

        // Firefox
        await CaptureWithBrowser(playwright.Firefox, "firefox", htmlPath, screenshotDir);
    }

    private static async Task CaptureWithBrowser(
        IBrowserType browserType, string browserName, string htmlPath, string outputDir)
    {
        await using var browser = await browserType.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync(new()
        {
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
        });

        await page.GotoAsync($"file:///{htmlPath.Replace('\\', '/')}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        // Wait for charts to render
        await page.WaitForTimeoutAsync(3000);

        // Full page screenshot
        await page.ScreenshotAsync(new()
        {
            Path = Path.Combine(outputDir, $"report-fullpage-{browserName}.png"),
            FullPage = true
        });

        // Viewport-only screenshot (above the fold)
        await page.ScreenshotAsync(new()
        {
            Path = Path.Combine(outputDir, $"report-viewport-{browserName}.png"),
            FullPage = false
        });

        // Score cards section
        var scoreSection = page.Locator("#scores-section");
        if (await scoreSection.CountAsync() > 0)
        {
            await scoreSection.ScreenshotAsync(new()
            {
                Path = Path.Combine(outputDir, $"report-scores-{browserName}.png")
            });
        }

        // Charts section - CPU chart
        var cpuChart = page.Locator("#chart-cpu");
        if (await cpuChart.CountAsync() > 0)
        {
            // Scroll to it first to ensure it's rendered
            await cpuChart.ScrollIntoViewIfNeededAsync();
            await page.WaitForTimeoutAsync(1000);
            await cpuChart.ScreenshotAsync(new()
            {
                Path = Path.Combine(outputDir, $"report-chart-cpu-{browserName}.png")
            });
        }

        // Memory chart
        var memChart = page.Locator("#chart-memory");
        if (await memChart.CountAsync() > 0)
        {
            await memChart.ScrollIntoViewIfNeededAsync();
            await page.WaitForTimeoutAsync(1000);
            await memChart.ScreenshotAsync(new()
            {
                Path = Path.Combine(outputDir, $"report-chart-memory-{browserName}.png")
            });
        }

        // Hardware inventory section
        var hwSection = page.Locator("#hardware-section");
        if (await hwSection.CountAsync() > 0)
        {
            await hwSection.ScrollIntoViewIfNeededAsync();
            await page.WaitForTimeoutAsync(500);
            await hwSection.ScreenshotAsync(new()
            {
                Path = Path.Combine(outputDir, $"report-hardware-{browserName}.png")
            });
        }

        // System config section
        var configSection = page.Locator("#sysconfig-section");
        if (await configSection.CountAsync() > 0)
        {
            await configSection.ScrollIntoViewIfNeededAsync();
            await page.WaitForTimeoutAsync(500);
            await configSection.ScreenshotAsync(new()
            {
                Path = Path.Combine(outputDir, $"report-sysconfig-{browserName}.png")
            });
        }

        // Sensor health section
        var healthSection = page.Locator("#sensor-health-section");
        if (await healthSection.CountAsync() > 0)
        {
            await healthSection.ScrollIntoViewIfNeededAsync();
            await page.WaitForTimeoutAsync(500);
            await healthSection.ScreenshotAsync(new()
            {
                Path = Path.Combine(outputDir, $"report-sensor-health-{browserName}.png")
            });
        }

        // Recommendations section
        var recSection = page.Locator("#recommendations-section");
        if (await recSection.CountAsync() > 0)
        {
            var isHidden = await recSection.EvaluateAsync<bool>("el => el.classList.contains('section-hidden')");
            if (!isHidden)
            {
                await recSection.ScrollIntoViewIfNeededAsync();
                await page.WaitForTimeoutAsync(500);
                await recSection.ScreenshotAsync(new()
                {
                    Path = Path.Combine(outputDir, $"report-recommendations-{browserName}.png")
                });
            }
        }

        // Scores section
        var scoresSection = page.Locator("#scores-section");
        if (await scoresSection.CountAsync() > 0)
        {
            await scoresSection.ScrollIntoViewIfNeededAsync();
            await page.WaitForTimeoutAsync(500);
            await scoresSection.ScreenshotAsync(new()
            {
                Path = Path.Combine(outputDir, $"report-scores-{browserName}.png")
            });
        }
    }
}
