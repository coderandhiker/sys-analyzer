using FluentAssertions;
using SysAnalyzer.Config.ExpressionEngine;

namespace SysAnalyzer.Tests.Unit;

public class ExpressionEvaluatorTests
{
    private static bool Eval(string expression, Dictionary<string, object?> fields)
    {
        var ast = ExpressionParser.Parse(expression);
        var evaluator = new ExpressionEvaluator(fields);
        return evaluator.Evaluate(ast);
    }

    [Fact]
    public void Evaluate_NumberGreaterThan_True()
    {
        var fields = new Dictionary<string, object?> { ["cpu.load"] = 95.0 };
        Eval("cpu.load > 90", fields).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_NumberGreaterThan_False()
    {
        var fields = new Dictionary<string, object?> { ["cpu.load"] = 80.0 };
        Eval("cpu.load > 90", fields).Should().BeFalse();
    }

    [Fact]
    public void Evaluate_MissingField_ReturnsFalse()
    {
        var fields = new Dictionary<string, object?>();
        Eval("cpu.load > 90", fields).Should().BeFalse();
    }

    [Fact]
    public void Evaluate_NullField_ReturnsFalse()
    {
        var fields = new Dictionary<string, object?> { ["cpu.load"] = null };
        Eval("cpu.load > 90", fields).Should().BeFalse();
    }

    [Fact]
    public void Evaluate_StringEquality_True()
    {
        var fields = new Dictionary<string, object?> { ["system.power_plan"] = "Balanced" };
        Eval("system.power_plan == 'Balanced'", fields).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_StringEquality_False()
    {
        var fields = new Dictionary<string, object?> { ["system.power_plan"] = "High performance" };
        Eval("system.power_plan == 'Balanced'", fields).Should().BeFalse();
    }

    [Fact]
    public void Evaluate_TypeMismatch_ThrowsError()
    {
        var fields = new Dictionary<string, object?> { ["cpu.model"] = "AMD Ryzen 7" };
        var act = () => Eval("cpu.model > 90", fields);
        act.Should().Throw<ExpressionEvaluationException>()
            .WithMessage("*Type mismatch*");
    }

    [Fact]
    public void Evaluate_ShortCircuitAnd_FirstFalse_NoError()
    {
        // First operand is false, second has missing field that would cause issues
        // But short-circuit means we never evaluate the second
        var fields = new Dictionary<string, object?> { ["a"] = false };
        Eval("a AND b > 5", fields).Should().BeFalse();
    }

    [Fact]
    public void Evaluate_ShortCircuitOr_FirstTrue_NoError()
    {
        var fields = new Dictionary<string, object?> { ["a"] = true };
        Eval("a OR b > 5", fields).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_BareFieldTruthy_BoolTrue()
    {
        var fields = new Dictionary<string, object?> { ["frametime.has_data"] = true };
        Eval("frametime.has_data", fields).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_BareFieldTruthy_BoolFalse()
    {
        var fields = new Dictionary<string, object?> { ["frametime.has_data"] = false };
        Eval("frametime.has_data", fields).Should().BeFalse();
    }

    [Fact]
    public void Evaluate_BareFieldTruthy_PositiveDouble()
    {
        var fields = new Dictionary<string, object?> { ["cpu.load"] = 50.0 };
        Eval("cpu.load", fields).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_BareFieldTruthy_ZeroDouble()
    {
        var fields = new Dictionary<string, object?> { ["cpu.load"] = 0.0 };
        Eval("cpu.load", fields).Should().BeFalse();
    }

    [Fact]
    public void Evaluate_BareFieldTruthy_NonEmptyString()
    {
        var fields = new Dictionary<string, object?> { ["system.power_plan"] = "Balanced" };
        Eval("system.power_plan", fields).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_BareFieldTruthy_EmptyString()
    {
        var fields = new Dictionary<string, object?> { ["system.power_plan"] = "" };
        Eval("system.power_plan", fields).Should().BeFalse();
    }

    [Fact]
    public void Evaluate_BareFieldTruthy_Missing()
    {
        var fields = new Dictionary<string, object?>();
        Eval("frametime.has_data", fields).Should().BeFalse();
    }

    [Fact]
    public void Evaluate_NotExpression()
    {
        var fields = new Dictionary<string, object?> { ["gpu.has_data"] = false };
        Eval("NOT gpu.has_data", fields).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_CompoundAndOr()
    {
        var fields = new Dictionary<string, object?>
        {
            ["a"] = 5.0,
            ["b"] = 1.0,
            ["c"] = "foo"
        };
        // (a > 1 AND b < 2) OR (c == 'bar')
        Eval("a > 1 AND b < 2 OR c == 'bar'", fields).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_IntFieldAsNumber()
    {
        var fields = new Dictionary<string, object?> { ["memory.slots_available"] = 2 };
        Eval("memory.slots_available > 0", fields).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_BoolEquality()
    {
        var fields = new Dictionary<string, object?> { ["disk.os_drive_is_hdd"] = true };
        Eval("disk.os_drive_is_hdd == true", fields).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_LessThanOrEqual()
    {
        var fields = new Dictionary<string, object?> { ["cpu.load"] = 90.0 };
        Eval("cpu.load <= 90", fields).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_GreaterThanOrEqual()
    {
        var fields = new Dictionary<string, object?> { ["cpu.load"] = 90.0 };
        Eval("cpu.load >= 90", fields).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_StringNotEqual()
    {
        var fields = new Dictionary<string, object?> { ["system.power_plan"] = "Balanced" };
        Eval("system.power_plan != 'High performance'", fields).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_StringGreaterThan_ThrowsError()
    {
        var fields = new Dictionary<string, object?> { ["a"] = "hello", ["b"] = "world" };
        var act = () => Eval("a > 'world'", fields);
        act.Should().Throw<ExpressionEvaluationException>();
    }
}
