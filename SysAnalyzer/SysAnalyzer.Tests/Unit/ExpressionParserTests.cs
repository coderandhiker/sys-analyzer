using SysAnalyzer.Config.ExpressionEngine;

namespace SysAnalyzer.Tests.Unit;

public class ExpressionParserTests
{
    [Fact]
    public void Parse_SimpleComparison_ProducesComparisonNode()
    {
        var node = ExpressionParser.Parse("cpu.load > 90");
        var cmp = Assert.IsType<ComparisonExpression>(node);
        var left = Assert.IsType<FieldRefNode>(cmp.Left);
        Assert.Equal("cpu.load", left.FieldPath);
        Assert.Equal(TokenType.GreaterThan, cmp.Operator);
        var right = Assert.IsType<NumberLiteralNode>(cmp.Right);
        Assert.Equal(90, right.Value);
    }

    [Fact]
    public void Parse_AndExpression_MultipleOperands()
    {
        var node = ExpressionParser.Parse("cpu.load > 90 AND memory.used > 80");
        var and = Assert.IsType<AndExpression>(node);
        Assert.Equal(2, and.Operands.Count);
    }

    [Fact]
    public void Parse_OrExpression_MultipleOperands()
    {
        var node = ExpressionParser.Parse("cpu.load > 90 OR gpu.load > 95");
        var or = Assert.IsType<OrExpression>(node);
        Assert.Equal(2, or.Operands.Count);
    }

    [Fact]
    public void Parse_NotExpression_Wraps()
    {
        var node = ExpressionParser.Parse("NOT cpu.throttled");
        var not = Assert.IsType<NotExpression>(node);
        Assert.IsType<FieldRefNode>(not.Operand);
    }

    [Fact]
    public void Parse_ComplexExpression_MixedAndOr()
    {
        var node = ExpressionParser.Parse("cpu.load > 90 AND gpu.load > 80 OR disk.active > 95");
        Assert.IsType<OrExpression>(node);
    }

    [Fact]
    public void Parse_StringLiteral()
    {
        var node = ExpressionParser.Parse("power.plan == 'High performance'");
        var cmp = Assert.IsType<ComparisonExpression>(node);
        var right = Assert.IsType<StringLiteralNode>(cmp.Right);
        Assert.Equal("High performance", right.Value);
    }

    [Fact]
    public void Parse_BoolLiteral()
    {
        var node = ExpressionParser.Parse("system.hags == true");
        var cmp = Assert.IsType<ComparisonExpression>(node);
        var right = Assert.IsType<BoolLiteralNode>(cmp.Right);
        Assert.True(right.Value);
    }

    [Fact]
    public void Parse_EmptyExpression_Throws()
    {
        Assert.Throws<ExpressionParseException>(() => ExpressionParser.Parse(""));
    }

    [Fact]
    public void Parse_InvalidCharacter_Throws()
    {
        Assert.Throws<ExpressionParseException>(() => ExpressionParser.Parse("cpu.load # 90"));
    }

    [Fact]
    public void GetFieldReferences_ReturnsAllFields()
    {
        var node = ExpressionParser.Parse("cpu.load > 90 AND memory.used > 80");
        var fields = ExpressionParser.GetFieldReferences(node);
        Assert.Contains("cpu.load", fields);
        Assert.Contains("memory.used", fields);
    }

    [Fact]
    public void Parse_AllComparisonOperators()
    {
        var operators = new[] { ">", "<", ">=", "<=", "==", "!=" };
        foreach (var op in operators)
        {
            var node = ExpressionParser.Parse($"cpu.load {op} 90");
            Assert.IsType<ComparisonExpression>(node);
        }
    }

    [Fact]
    public void Parse_NegativeNumber()
    {
        var node = ExpressionParser.Parse("temp.delta > -5");
        var cmp = Assert.IsType<ComparisonExpression>(node);
        var right = Assert.IsType<NumberLiteralNode>(cmp.Right);
        Assert.Equal(-5, right.Value);
    }

    [Fact]
    public void Parse_DecimalNumber()
    {
        var node = ExpressionParser.Parse("cpu.load > 95.5");
        var cmp = Assert.IsType<ComparisonExpression>(node);
        var right = Assert.IsType<NumberLiteralNode>(cmp.Right);
        Assert.Equal(95.5, right.Value);
    }
}
