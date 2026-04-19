using SysAnalyzer.Config;

namespace SysAnalyzer.Tests.Unit;

public class ConfigValidatorTests
{
    private readonly ConfigValidator _validator = new();

    [Fact]
    public void Validate_DefaultConfig_NoErrors()
    {
        var config = ConfigLoader.Load();
        var result = _validator.Validate(config);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_PollIntervalTooLow_Error()
    {
        var config = ConfigLoader.Load();
        config.Capture.PollIntervalMs = 50;
        var result = _validator.Validate(config);
        Assert.Contains(result.Errors, e => e.Message.Contains("poll_interval_ms"));
    }

    [Fact]
    public void Validate_MinCaptureTooLow_Error()
    {
        var config = ConfigLoader.Load();
        config.Capture.MinCaptureDurationSec = 0;
        var result = _validator.Validate(config);
        Assert.Contains(result.Errors, e => e.Message.Contains("min_capture_duration_sec"));
    }

    [Fact]
    public void Validate_DuplicateRecommendationId_Error()
    {
        var config = new AnalyzerConfig();
        config.Recommendations.Add(new RecommendationConfig
        {
            Id = "dup", Trigger = "cpu.load > 90", Severity = "info",
            Category = "cpu", Confidence = "low", Title = "T", Body = "B"
        });
        config.Recommendations.Add(new RecommendationConfig
        {
            Id = "dup", Trigger = "cpu.load > 95", Severity = "warning",
            Category = "cpu", Confidence = "low", Title = "T2", Body = "B2"
        });
        var result = _validator.Validate(config);
        Assert.Contains(result.Errors, e => e.Message.Contains("duplicate"));
    }

    [Fact]
    public void Validate_InvalidSeverity_Error()
    {
        var config = new AnalyzerConfig();
        config.Recommendations.Add(new RecommendationConfig
        {
            Id = "test", Trigger = "cpu.load > 90", Severity = "emergency",
            Category = "cpu", Confidence = "low", Title = "T", Body = "B"
        });
        var result = _validator.Validate(config);
        Assert.Contains(result.Errors, e => e.Message.Contains("severity"));
    }

    [Fact]
    public void Validate_EmptyTrigger_Error()
    {
        var config = new AnalyzerConfig();
        config.Recommendations.Add(new RecommendationConfig
        {
            Id = "test", Trigger = "", Severity = "info",
            Category = "cpu", Confidence = "low", Title = "T", Body = "B"
        });
        var result = _validator.Validate(config);
        Assert.Contains(result.Errors, e => e.Message.Contains("trigger"));
    }

    [Fact]
    public void Validate_InvalidCategory_Error()
    {
        var config = new AnalyzerConfig();
        config.Recommendations.Add(new RecommendationConfig
        {
            Id = "test", Trigger = "cpu.load > 90", Severity = "info",
            Category = "imaginary", Confidence = "low", Title = "T", Body = "B"
        });
        var result = _validator.Validate(config);
        Assert.Contains(result.Errors, e => e.Message.Contains("category"));
    }

    [Fact]
    public void Validate_InvalidConfidence_Error()
    {
        var config = new AnalyzerConfig();
        config.Recommendations.Add(new RecommendationConfig
        {
            Id = "test", Trigger = "cpu.load > 90", Severity = "info",
            Category = "cpu", Confidence = "absolute", Title = "T", Body = "B"
        });
        var result = _validator.Validate(config);
        Assert.Contains(result.Errors, e => e.Message.Contains("confidence"));
    }
}
