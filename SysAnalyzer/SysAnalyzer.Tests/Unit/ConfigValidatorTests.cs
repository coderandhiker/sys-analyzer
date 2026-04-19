using FluentAssertions;
using SysAnalyzer.Config;

namespace SysAnalyzer.Tests.Unit;

public class ConfigValidatorTests
{
    private readonly ConfigValidator _validator = new();

    [Fact]
    public void ValidConfig_LoadsWithoutErrors()
    {
        var config = ConfigLoader.Load();
        var result = _validator.Validate(config);
        result.IsValid.Should().BeTrue($"Expected no errors but got: {string.Join("; ", result.Errors.Select(e => $"{e.Context}: {e.Message}"))}");
    }

    [Fact]
    public void DuplicateRecommendationId_ReportsError()
    {
        var config = new AnalyzerConfig
        {
            Recommendations = new List<RecommendationConfig>
            {
                new() { Id = "dup_id", Trigger = "cpu.load > 90", Severity = "warning", Category = "cpu" },
                new() { Id = "dup_id", Trigger = "memory.used > 80", Severity = "warning", Category = "memory" }
            }
        };
        var result = _validator.Validate(config);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("duplicate"));
    }

    [Fact]
    public void BadSeverity_ReportsError()
    {
        var config = new AnalyzerConfig
        {
            Recommendations = new List<RecommendationConfig>
            {
                new() { Id = "test", Trigger = "cpu.load > 90", Severity = "extreme", Category = "cpu" }
            }
        };
        var result = _validator.Validate(config);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("severity") && e.Message.Contains("extreme"));
    }

    [Fact]
    public void BadCategory_ReportsError()
    {
        var config = new AnalyzerConfig
        {
            Recommendations = new List<RecommendationConfig>
            {
                new() { Id = "test", Trigger = "cpu.load > 90", Severity = "info", Category = "storage" }
            }
        };
        var result = _validator.Validate(config);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("category"));
    }

    [Fact]
    public void BadConfidence_ReportsError()
    {
        var config = new AnalyzerConfig
        {
            Recommendations = new List<RecommendationConfig>
            {
                new() { Id = "test", Trigger = "cpu.load > 90", Severity = "info", Category = "cpu", Confidence = "maybe" }
            }
        };
        var result = _validator.Validate(config);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("confidence"));
    }

    [Fact]
    public void BadExpressionSyntax_ReportsPositionalError()
    {
        var config = new AnalyzerConfig
        {
            Recommendations = new List<RecommendationConfig>
            {
                new() { Id = "test", Trigger = "cpu.load >> 90", Severity = "warning", Category = "cpu" }
            }
        };
        var result = _validator.Validate(config);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Context.Contains("trigger") && e.Message.Contains("parse error"));
    }

    [Fact]
    public void EmptyTrigger_ReportsError()
    {
        var config = new AnalyzerConfig
        {
            Recommendations = new List<RecommendationConfig>
            {
                new() { Id = "test", Trigger = "", Severity = "warning", Category = "cpu" }
            }
        };
        var result = _validator.Validate(config);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("trigger"));
    }

    [Fact]
    public void UnknownFieldReference_ReportsWarning()
    {
        var config = new AnalyzerConfig
        {
            Recommendations = new List<RecommendationConfig>
            {
                new() { Id = "test", Trigger = "foo.bar > 90", Severity = "warning", Category = "cpu" }
            }
        };
        var result = _validator.Validate(config);
        result.Warnings.Should().Contain(w => w.Message.Contains("unknown field") && w.Message.Contains("foo.bar"));
    }

    [Fact]
    public void BadPlaceholder_ReportsErrorWithSuggestion()
    {
        var config = new AnalyzerConfig
        {
            Recommendations = new List<RecommendationConfig>
            {
                new() { Id = "test", Trigger = "cpu.load > 90", Severity = "warning", Category = "cpu",
                    Body = "The {cpx.model} is overheating" }
            }
        };
        var result = _validator.Validate(config);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("cpx.model") && e.Message.Contains("did you mean"));
    }

    [Fact]
    public void MissingId_ReportsError()
    {
        var config = new AnalyzerConfig
        {
            Recommendations = new List<RecommendationConfig>
            {
                new() { Id = "", Trigger = "cpu.load > 90", Severity = "warning", Category = "cpu" }
            }
        };
        var result = _validator.Validate(config);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("id is required"));
    }

    [Fact]
    public void MultipleErrors_AllReported()
    {
        var config = new AnalyzerConfig
        {
            Recommendations = new List<RecommendationConfig>
            {
                new() { Id = "a", Trigger = "cpu.load >> 90", Severity = "extreme", Category = "storage" },
                new() { Id = "a", Trigger = "valid > 1", Severity = "info", Category = "cpu" }
            }
        };
        var result = _validator.Validate(config);
        result.IsValid.Should().BeFalse();
        // Should have at least 3 errors: bad trigger, bad severity, bad category, duplicate id
        result.Errors.Should().HaveCountGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void ValidEvidenceBoost_NoError()
    {
        var config = new AnalyzerConfig
        {
            Recommendations = new List<RecommendationConfig>
            {
                new() { Id = "test", Trigger = "cpu.load > 90", Severity = "warning", Category = "cpu",
                    Confidence = "auto", EvidenceBoost = "frametime.has_data AND frametime.stutter_correlates_with_pagefault" }
            }
        };
        var result = _validator.Validate(config);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void BadEvidenceBoost_ReportsError()
    {
        var config = new AnalyzerConfig
        {
            Recommendations = new List<RecommendationConfig>
            {
                new() { Id = "test", Trigger = "cpu.load > 90", Severity = "warning", Category = "cpu",
                    EvidenceBoost = "bad >> syntax" }
            }
        };
        var result = _validator.Validate(config);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Context.Contains("evidence_boost"));
    }
}
