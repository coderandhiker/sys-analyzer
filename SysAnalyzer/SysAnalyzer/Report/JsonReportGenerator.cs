using System.Text.Json;
using System.Text.Json.Serialization;

namespace SysAnalyzer.Report;

/// <summary>
/// JSON serialization settings and helpers for AnalysisSummary.
/// </summary>
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
}
