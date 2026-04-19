using System.Text.RegularExpressions;

namespace SysAnalyzer.Report;

public static partial class FilenameGenerator
{
    public static string Generate(string format, string timestampFormat, string? label = null)
    {
        var result = format;

        // Replace {timestamp} with formatted timestamp
        result = result.Replace("{timestamp}", DateTime.Now.ToString(timestampFormat));

        // Replace {label} if provided
        if (label != null)
        {
            result = result.Replace("{label}", SanitizeLabel(label));
        }
        else
        {
            result = result.Replace("-{label}", "").Replace("{label}", "");
        }

        // Final sanitization — remove any remaining invalid filename characters
        return SanitizeFilename(result);
    }

    public static string SanitizeLabel(string label)
    {
        // Replace spaces with hyphens
        var sanitized = label.Replace(' ', '-');
        // Strip invalid filename characters
        sanitized = InvalidCharsRegex().Replace(sanitized, "");
        return sanitized.ToLowerInvariant();
    }

    private static string SanitizeFilename(string filename)
    {
        return InvalidCharsRegex().Replace(filename, "");
    }

    [GeneratedRegex(@"[<>:""/\\|?*]")]
    private static partial Regex InvalidCharsRegex();
}
