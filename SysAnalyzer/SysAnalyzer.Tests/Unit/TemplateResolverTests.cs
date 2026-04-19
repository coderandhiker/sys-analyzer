using SysAnalyzer.Config.ExpressionEngine;

namespace SysAnalyzer.Tests.Unit;

public class TemplateResolverTests
{
    [Fact]
    public void Resolve_DoubleField_OneDecimalPlace()
    {
        var fields = new Dictionary<string, object?> { ["cpu.load"] = 78.567 };
        var result = TemplateResolver.Resolve("CPU load is {cpu.load}%", fields);
        Assert.Equal("CPU load is 78.6%", result);
    }

    [Fact]
    public void Resolve_BoolField_YesNo()
    {
        var fields = new Dictionary<string, object?> { ["hags.enabled"] = true };
        var result = TemplateResolver.Resolve("HAGS: {hags.enabled}", fields);
        Assert.Equal("HAGS: Yes", result);
    }

    [Fact]
    public void Resolve_BoolFalse_No()
    {
        var fields = new Dictionary<string, object?> { ["hags.enabled"] = false };
        var result = TemplateResolver.Resolve("HAGS: {hags.enabled}", fields);
        Assert.Equal("HAGS: No", result);
    }

    [Fact]
    public void Resolve_MissingField_Unknown()
    {
        var fields = new Dictionary<string, object?>();
        var result = TemplateResolver.Resolve("CPU: {cpu.model}", fields);
        Assert.Equal("CPU: [unknown]", result);
    }

    [Fact]
    public void Resolve_NullField_Unknown()
    {
        var fields = new Dictionary<string, object?> { ["cpu.model"] = null };
        var result = TemplateResolver.Resolve("CPU: {cpu.model}", fields);
        Assert.Equal("CPU: [unknown]", result);
    }

    [Fact]
    public void Resolve_StringField_PassedThrough()
    {
        var fields = new Dictionary<string, object?> { ["cpu.model"] = "AMD Ryzen 7 5800X" };
        var result = TemplateResolver.Resolve("CPU: {cpu.model}", fields);
        Assert.Equal("CPU: AMD Ryzen 7 5800X", result);
    }

    [Fact]
    public void Resolve_IntField_NoDecimal()
    {
        var fields = new Dictionary<string, object?> { ["cpu.cores"] = 8 };
        var result = TemplateResolver.Resolve("Cores: {cpu.cores}", fields);
        Assert.Equal("Cores: 8", result);
    }

    [Fact]
    public void Resolve_MultipleFields()
    {
        var fields = new Dictionary<string, object?>
        {
            ["cpu.load"] = 95.5,
            ["gpu.load"] = 80.3
        };
        var result = TemplateResolver.Resolve("CPU: {cpu.load}%, GPU: {gpu.load}%", fields);
        Assert.Equal("CPU: 95.5%, GPU: 80.3%", result);
    }

    [Fact]
    public void GetPlaceholders_ReturnsAllFieldNames()
    {
        var placeholders = TemplateResolver.GetPlaceholders("CPU: {cpu.load}%, GPU: {gpu.load}%");
        Assert.Equal(2, placeholders.Count);
        Assert.Contains("cpu.load", placeholders);
        Assert.Contains("gpu.load", placeholders);
    }

    [Fact]
    public void Resolve_NoPlaceholders_ReturnsSame()
    {
        var fields = new Dictionary<string, object?>();
        var result = TemplateResolver.Resolve("No placeholders here", fields);
        Assert.Equal("No placeholders here", result);
    }
}
