using FluentAssertions;
using SysAnalyzer.Config.ExpressionEngine;

namespace SysAnalyzer.Tests.Unit;

public class TemplateResolverTests
{
    [Fact]
    public void Resolve_BasicSubstitution()
    {
        var fields = new Dictionary<string, object?>
        {
            ["cpu.model"] = "AMD Ryzen 7 5800X",
            ["cpu.load"] = 95.1
        };
        var result = TemplateResolver.Resolve("Your {cpu.model} at {cpu.load}%", fields);
        result.Should().Be("Your AMD Ryzen 7 5800X at 95.1%");
    }

    [Fact]
    public void Resolve_MissingField_ReturnsUnknown()
    {
        var fields = new Dictionary<string, object?>();
        var result = TemplateResolver.Resolve("Your {cpu.model} at {cpu.load}%", fields);
        result.Should().Be("Your [unknown] at [unknown]%");
    }

    [Fact]
    public void Resolve_NullField_ReturnsUnknown()
    {
        var fields = new Dictionary<string, object?> { ["cpu.model"] = null };
        var result = TemplateResolver.Resolve("CPU: {cpu.model}", fields);
        result.Should().Be("CPU: [unknown]");
    }

    [Fact]
    public void Resolve_BoolTrue_ReturnsYes()
    {
        var fields = new Dictionary<string, object?> { ["system.game_mode"] = true };
        var result = TemplateResolver.Resolve("Game Mode: {system.game_mode}", fields);
        result.Should().Be("Game Mode: Yes");
    }

    [Fact]
    public void Resolve_BoolFalse_ReturnsNo()
    {
        var fields = new Dictionary<string, object?> { ["system.game_mode"] = false };
        var result = TemplateResolver.Resolve("Game Mode: {system.game_mode}", fields);
        result.Should().Be("Game Mode: No");
    }

    [Fact]
    public void Resolve_Double_OneDecimalPlace()
    {
        var fields = new Dictionary<string, object?> { ["cpu.temp"] = 82.678 };
        var result = TemplateResolver.Resolve("Temp: {cpu.temp}°C", fields);
        result.Should().Be("Temp: 82.7°C");
    }

    [Fact]
    public void Resolve_Integer_NoDecimalPlace()
    {
        var fields = new Dictionary<string, object?> { ["memory.total_gb"] = 32 };
        var result = TemplateResolver.Resolve("{memory.total_gb}GB", fields);
        result.Should().Be("32GB");
    }

    [Fact]
    public void Resolve_NoPlaceholders_ReturnsOriginal()
    {
        var fields = new Dictionary<string, object?>();
        var result = TemplateResolver.Resolve("No placeholders here", fields);
        result.Should().Be("No placeholders here");
    }

    [Fact]
    public void GetPlaceholders_FindsAll()
    {
        var placeholders = TemplateResolver.GetPlaceholders("Your {cpu.model} at {cpu.load}% with {memory.total_gb}GB");
        placeholders.Should().BeEquivalentTo(new[] { "cpu.model", "cpu.load", "memory.total_gb" });
    }
}
