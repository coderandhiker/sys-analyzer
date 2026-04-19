using System.Globalization;
using System.Text.RegularExpressions;

namespace SysAnalyzer.Config.ExpressionEngine;

/// <summary>
/// Resolves {field.path} placeholders in recommendation body templates.
/// Doubles: 1 decimal place. Bools: "Yes"/"No". Missing: "[unknown]".
/// </summary>
public static partial class TemplateResolver
{
    private static readonly Regex PlaceholderPattern = PlaceholderRegex();

    public static string Resolve(string template, Dictionary<string, object?> fields)
    {
        return PlaceholderPattern.Replace(template, match =>
        {
            var fieldPath = match.Groups[1].Value;
            if (!fields.TryGetValue(fieldPath, out var value) || value is null)
                return "[unknown]";

            return value switch
            {
                double d => d.ToString("F1", CultureInfo.InvariantCulture),
                float f => f.ToString("F1", CultureInfo.InvariantCulture),
                int i => i.ToString(CultureInfo.InvariantCulture),
                long l => l.ToString(CultureInfo.InvariantCulture),
                bool b => b ? "Yes" : "No",
                string s => s,
                _ => value.ToString() ?? "[unknown]"
            };
        });
    }

    /// <summary>
    /// Extract all placeholder field names from a template.
    /// </summary>
    public static IReadOnlyList<string> GetPlaceholders(string template)
    {
        return PlaceholderPattern.Matches(template)
            .Select(m => m.Groups[1].Value)
            .ToList();
    }

    [GeneratedRegex(@"\{([a-zA-Z_][a-zA-Z0-9_.]*)\}")]
    private static partial Regex PlaceholderRegex();
}
