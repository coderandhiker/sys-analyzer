using SysAnalyzer.Config.ExpressionEngine;

namespace SysAnalyzer.Config;

/// <summary>
/// Validates a loaded AnalyzerConfig. Reports ALL errors at once (§6.3).
/// </summary>
public sealed class ConfigValidator
{
    private static readonly HashSet<string> ValidSeverities = new(StringComparer.OrdinalIgnoreCase)
        { "info", "warning", "critical" };

    private static readonly HashSet<string> ValidCategories = new(StringComparer.OrdinalIgnoreCase)
        { "cpu", "memory", "gpu", "disk", "network", "software", "config", "frametime" };

    private static readonly HashSet<string> ValidConfidences = new(StringComparer.OrdinalIgnoreCase)
        { "high", "medium", "low", "auto" };

    // Known field prefixes for warning on unknown references
    private static readonly HashSet<string> KnownFieldPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "cpu", "memory", "gpu", "disk", "network", "frametime", "system",
        "culprit", "thresholds", "motherboard"
    };

    public ConfigValidationResult Validate(AnalyzerConfig config)
    {
        var errors = new List<ConfigValidationError>();
        var warnings = new List<ConfigValidationError>();

        ValidateCapture(config.Capture, errors);
        ValidateRecommendations(config.Recommendations, errors, warnings);

        return new ConfigValidationResult(errors, warnings);
    }

    private static void ValidateCapture(CaptureConfig capture, List<ConfigValidationError> errors)
    {
        if (capture.PollIntervalMs < 100)
            errors.Add(new ConfigValidationError("capture", "poll_interval_ms must be >= 100"));
        if (capture.MinCaptureDurationSec < 1)
            errors.Add(new ConfigValidationError("capture", "min_capture_duration_sec must be >= 1"));
    }

    private void ValidateRecommendations(List<RecommendationConfig> recommendations,
        List<ConfigValidationError> errors, List<ConfigValidationError> warnings)
    {
        var seenIds = new HashSet<string>();

        for (int i = 0; i < recommendations.Count; i++)
        {
            var rec = recommendations[i];
            var prefix = $"recommendations[{i}] (id: {rec.Id})";

            // Unique ID
            if (string.IsNullOrWhiteSpace(rec.Id))
            {
                errors.Add(new ConfigValidationError(prefix, "recommendation id is required"));
            }
            else if (!seenIds.Add(rec.Id))
            {
                errors.Add(new ConfigValidationError(prefix, $"duplicate recommendation id '{rec.Id}'"));
            }

            // Severity
            if (!ValidSeverities.Contains(rec.Severity))
                errors.Add(new ConfigValidationError(prefix,
                    $"invalid severity '{rec.Severity}'. Must be: info, warning, critical"));

            // Category
            if (!ValidCategories.Contains(rec.Category))
                errors.Add(new ConfigValidationError(prefix,
                    $"invalid category '{rec.Category}'. Must be: cpu, memory, gpu, disk, network, software, config, frametime"));

            // Confidence
            if (!ValidConfidences.Contains(rec.Confidence))
                errors.Add(new ConfigValidationError(prefix,
                    $"invalid confidence '{rec.Confidence}'. Must be: high, medium, low, auto"));

            // Parse trigger expression
            if (string.IsNullOrWhiteSpace(rec.Trigger))
            {
                errors.Add(new ConfigValidationError(prefix, "trigger expression is required"));
            }
            else
            {
                ValidateExpression(rec.Trigger, $"{prefix} trigger", errors, warnings);
            }

            // Parse evidence_boost if present
            if (!string.IsNullOrWhiteSpace(rec.EvidenceBoost))
            {
                ValidateExpression(rec.EvidenceBoost, $"{prefix} evidence_boost", errors, warnings);
            }

            // Validate body placeholders
            if (!string.IsNullOrWhiteSpace(rec.Body))
            {
                ValidateBodyPlaceholders(rec.Body, prefix, errors, warnings);
            }
        }
    }

    private void ValidateExpression(string expression, string context,
        List<ConfigValidationError> errors, List<ConfigValidationError> warnings)
    {
        try
        {
            var ast = ExpressionParser.Parse(expression);
            var fields = ExpressionParser.GetFieldReferences(ast);

            foreach (var field in fields)
            {
                var topLevel = field.Split('.')[0];
                if (!KnownFieldPrefixes.Contains(topLevel))
                {
                    warnings.Add(new ConfigValidationError(context,
                        $"references unknown field '{field}'"));
                }
            }
        }
        catch (ExpressionParseException ex)
        {
            errors.Add(new ConfigValidationError(context,
                $"parse error at position {ex.Position}: {ex.Message}"));
        }
    }

    private static void ValidateBodyPlaceholders(string body, string context,
        List<ConfigValidationError> errors, List<ConfigValidationError> warnings)
    {
        var placeholders = TemplateResolver.GetPlaceholders(body);
        foreach (var placeholder in placeholders)
        {
            var topLevel = placeholder.Split('.')[0];
            if (!KnownFieldPrefixes.Contains(topLevel))
            {
                // Try to suggest a correction
                var suggestion = FindClosestMatch(topLevel, KnownFieldPrefixes);
                var msg = suggestion is not null
                    ? $"unknown placeholder {{{placeholder}}} — did you mean {{{suggestion}.{string.Join(".", placeholder.Split('.')[1..])}}}?"
                    : $"unknown placeholder {{{placeholder}}}";
                errors.Add(new ConfigValidationError($"{context} body", msg));
            }
        }
    }

    private static string? FindClosestMatch(string input, HashSet<string> candidates)
    {
        string? best = null;
        int bestDist = int.MaxValue;
        foreach (var candidate in candidates)
        {
            var dist = LevenshteinDistance(input.ToLowerInvariant(), candidate.ToLowerInvariant());
            if (dist < bestDist && dist <= 3)
            {
                bestDist = dist;
                best = candidate;
            }
        }
        return best;
    }

    private static int LevenshteinDistance(string s, string t)
    {
        int n = s.Length, m = t.Length;
        var d = new int[n + 1, m + 1];
        for (int i = 0; i <= n; i++) d[i, 0] = i;
        for (int j = 0; j <= m; j++) d[0, j] = j;
        for (int i = 1; i <= n; i++)
            for (int j = 1; j <= m; j++)
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + (s[i - 1] == t[j - 1] ? 0 : 1));
        return d[n, m];
    }
}

public record ConfigValidationError(string Context, string Message);

public record ConfigValidationResult(
    IReadOnlyList<ConfigValidationError> Errors,
    IReadOnlyList<ConfigValidationError> Warnings)
{
    public bool IsValid => Errors.Count == 0;
}
