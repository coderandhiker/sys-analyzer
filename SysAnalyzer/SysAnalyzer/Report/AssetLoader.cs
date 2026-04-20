using System.Reflection;

namespace SysAnalyzer.Report;

/// <summary>
/// Reads embedded CSS/JS assets from the assembly for self-contained HTML report generation.
/// </summary>
public static class AssetLoader
{
    private static readonly Assembly Assembly = typeof(AssetLoader).Assembly;

    private static readonly Dictionary<string, string> AssetNames = new()
    {
        ["tabler.min.css"] = "SysAnalyzer.Report.Assets.tabler.min.css",
        ["tabler.js"] = "SysAnalyzer.Report.Assets.tabler.js",
        ["tabler-theme.js"] = "SysAnalyzer.Report.Assets.tabler-theme.js",
        ["apexcharts.min.js"] = "SysAnalyzer.Report.Assets.apexcharts.min.js",
        ["report.css"] = "SysAnalyzer.Report.Assets.report.css",
        ["report-template.html"] = "SysAnalyzer.Report.Templates.report-template.html"
    };

    /// <summary>
    /// Loads an embedded resource by short name and returns its text content.
    /// </summary>
    public static string Load(string shortName)
    {
        if (!AssetNames.TryGetValue(shortName, out var resourceName))
            throw new ArgumentException($"Unknown asset: {shortName}");

        using var stream = Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Checks whether an embedded resource exists.
    /// </summary>
    public static bool Exists(string shortName) =>
        AssetNames.TryGetValue(shortName, out var resourceName) &&
        Assembly.GetManifestResourceStream(resourceName) is not null;
}
