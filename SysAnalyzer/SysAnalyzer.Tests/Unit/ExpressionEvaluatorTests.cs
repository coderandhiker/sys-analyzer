using SysAnalyzer.Config.ExpressionEngine;

namespace SysAnalyzer.Tests.Unit;

public class ExpressionEvaluatorTests
{
    [Fact]
    public void Evaluate_SimpleGreaterThan_True()
    {
        var fields = new Dictionary<string, object?> { ["cpu.load"] = 95.0 };
        var evaluator = new ExpressionEvaluator(fields);
        var node = ExpressionParser.Parse("cpu.load > 90");
        Assert.True(evaluator.Evaluate(node));
    }

    [Fact]
    public void Evaluate_SimpleGreaterThan_False()
    {
        var fields = new Dictionary<string, object?> { ["cpu.load"] = 50.0 };
        var evaluator = new ExpressionEvaluator(fields);
        var node = ExpressionParser.Parse("cpu.load > 90");
        Assert.False(evaluator.Evaluate(node));
    }

    [Fact]
    public void Evaluate_And_BothTrue()
    {
        var fields = new Dictionary<string, object?>
        {
            ["cpu.load"] = 95.0,
            ["memory.used"] = 85.0
        };
        var evaluator = new ExpressionEvaluator(fields);
        var node = ExpressionParser.Parse("cpu.load > 90 AND memory.used > 80");
        Assert.True(evaluator.Evaluate(node));
    }

    [Fact]
    public void Evaluate_And_OneFalse()
    {
        var fields = new Dictionary<string, object?>
        {
            ["cpu.load"] = 95.0,
            ["memory.used"] = 50.0
        };
        var evaluator = new ExpressionEvaluator(fields);
        var node = ExpressionParser.Parse("cpu.load > 90 AND memory.used > 80");
        Assert.False(evaluator.Evaluate(node));
    }

    [Fact]
    public void Evaluate_Or_OneTrue()
    {
        var fields = new Dictionary<string, object?>
        {
            ["cpu.load"] = 95.0,
            ["gpu.load"] = 50.0
        };
        var evaluator = new ExpressionEvaluator(fields);
        var node = ExpressionParser.Parse("cpu.load > 90 OR gpu.load > 95");
        Assert.True(evaluator.Evaluate(node));
    }

    [Fact]
    public void Evaluate_Not_InvertsResult()
    {
        var fields = new Dictionary<string, object?> { ["cpu.throttled"] = false };
        var evaluator = new ExpressionEvaluator(fields);
        var node = ExpressionParser.Parse("NOT cpu.throttled");
        Assert.True(evaluator.Evaluate(node));
    }

    [Fact]
    public void Evaluate_MissingField_False()
    {
        var fields = new Dictionary<string, object?>();
        var evaluator = new ExpressionEvaluator(fields);
        var node = ExpressionParser.Parse("cpu.load > 90");
        Assert.False(evaluator.Evaluate(node));
    }

    [Fact]
    public void Evaluate_NullField_False()
    {
        var fields = new Dictionary<string, object?> { ["cpu.load"] = null };
        var evaluator = new ExpressionEvaluator(fields);
        var node = ExpressionParser.Parse("cpu.load > 90");
        Assert.False(evaluator.Evaluate(node));
    }

    [Fact]
    public void Evaluate_StringEquals()
    {
        var fields = new Dictionary<string, object?> { ["power.plan"] = "High performance" };
        var evaluator = new ExpressionEvaluator(fields);
        var node = ExpressionParser.Parse("power.plan == 'High performance'");
        Assert.True(evaluator.Evaluate(node));
    }

    [Fact]
    public void Evaluate_BoolEquals()
    {
        var fields = new Dictionary<string, object?> { ["system.hags"] = true };
        var evaluator = new ExpressionEvaluator(fields);
        var node = ExpressionParser.Parse("system.hags == true");
        Assert.True(evaluator.Evaluate(node));
    }

    [Fact]
    public void Evaluate_IntegerPromotedToDouble()
    {
        var fields = new Dictionary<string, object?> { ["cpu.cores"] = 8 };
        var evaluator = new ExpressionEvaluator(fields);
        var node = ExpressionParser.Parse("cpu.cores > 4");
        Assert.True(evaluator.Evaluate(node));
    }

    [Fact]
    public void Evaluate_LessThan()
    {
        var fields = new Dictionary<string, object?> { ["disk.free_gb"] = 5.0 };
        var evaluator = new ExpressionEvaluator(fields);
        var node = ExpressionParser.Parse("disk.free_gb < 10");
        Assert.True(evaluator.Evaluate(node));
    }

    [Fact]
    public void Evaluate_TypeMismatch_Throws()
    {
        var fields = new Dictionary<string, object?> { ["cpu.load"] = 95.0 };
        var evaluator = new ExpressionEvaluator(fields);
        var node = ExpressionParser.Parse("cpu.load == 'text'");
        Assert.Throws<ExpressionEvaluationException>(() => evaluator.Evaluate(node));
    }

    [Fact]
    public void Evaluate_TruthyFieldRef_Double_Positive_True()
    {
        var fields = new Dictionary<string, object?> { ["cpu.load"] = 50.0 };
        var evaluator = new ExpressionEvaluator(fields);
        var node = ExpressionParser.Parse("cpu.load");
        Assert.True(evaluator.Evaluate(node));
    }

    [Fact]
    public void Evaluate_TruthyFieldRef_Double_Zero_False()
    {
        var fields = new Dictionary<string, object?> { ["cpu.load"] = 0.0 };
        var evaluator = new ExpressionEvaluator(fields);
        var node = ExpressionParser.Parse("cpu.load");
        Assert.False(evaluator.Evaluate(node));
    }
}
