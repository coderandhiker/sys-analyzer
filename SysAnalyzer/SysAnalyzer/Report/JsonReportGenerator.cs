using System.Text.Json;
using System.Text.Json.Serialization;

namespace SysAnalyzer.Report;

public static class JsonReportGenerator
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string Serialize(Analysis.Models.AnalysisSummary summary) =>
        JsonSerializer.Serialize(summary, Options);

    public static Analysis.Models.AnalysisSummary? Deserialize(string json) =>
        JsonSerializer.Deserialize<Analysis.Models.AnalysisSummary>(json, Options);

    public static async Task<string> WriteToFileAsync(
        Analysis.Models.AnalysisSummary summary,
        string outputDir,
        string filenameFormat,
        string timestampFormat,
        string? label = null)
    {
        Directory.CreateDirectory(outputDir);
        var filename = FilenameGenerator.Generate(filenameFormat, timestampFormat, label);
        var path = Path.Combine(outputDir, filename + ".json");
        var json = Serialize(summary);
        await File.WriteAllTextAsync(path, json);
        return path;
    }
}
