using System.Reflection;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SysAnalyzer.Config;

/// <summary>
/// Loads config.yaml with fallback chain: adjacent to exe → CWD → embedded default (§10.3).
/// </summary>
public static class ConfigLoader
{
    private const string ConfigFileName = "config.yaml";
    private const string EmbeddedResourceName = "SysAnalyzer.config.yaml";

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static AnalyzerConfig LoadDefault() => Load();
    public static AnalyzerConfig Load(string? explicitPath = null)
    {
        var yaml = LoadYamlText(explicitPath);
        return Deserializer.Deserialize<AnalyzerConfig>(yaml) ?? new AnalyzerConfig();
    }

    public static string LoadYamlText(string? explicitPath = null)
    {
        // 1. Explicit path
        if (explicitPath is not null)
        {
            if (!File.Exists(explicitPath))
                throw new FileNotFoundException($"Config file not found: {explicitPath}", explicitPath);
            return File.ReadAllText(explicitPath);
        }

        // 2. Adjacent to exe
        var exeDir = Path.GetDirectoryName(AppContext.BaseDirectory) ?? ".";
        var adjacentPath = Path.Combine(exeDir, ConfigFileName);
        if (File.Exists(adjacentPath))
            return File.ReadAllText(adjacentPath);

        // 3. CWD
        var cwdPath = Path.Combine(Environment.CurrentDirectory, ConfigFileName);
        if (File.Exists(cwdPath))
            return File.ReadAllText(cwdPath);

        // 4. Embedded default
        return LoadEmbeddedDefault();
    }

    private static string LoadEmbeddedDefault()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(EmbeddedResourceName);
        if (stream is null)
            throw new InvalidOperationException("Embedded default config.yaml not found. The application may be corrupted.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
