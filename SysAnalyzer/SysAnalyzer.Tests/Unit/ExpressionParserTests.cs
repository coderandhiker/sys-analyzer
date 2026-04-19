using FluentAssertions;
using SysAnalyzer.Config.ExpressionEngine;

namespace SysAnalyzer.Tests.Unit;

public class ExpressionParserTests
{
    [Fact]
    public void Parse_SimpleComparison()
    {
        var ast = ExpressionParser.Parse("cpu.load > 90");
        ast.Should().BeOfType<ComparisonExpression>();
        var cmp = (ComparisonExpression)ast;
        cmp.Left.Should().BeOfType<FieldRefNode>().Which.FieldPath.Should().Be("cpu.load");
        cmp.Operator.Should().Be(TokenType.GreaterThan);
        cmp.Right.Should().BeOfType<NumberLiteralNode>().Which.Value.Should().Be(90.0);
    }

    [Fact]
    public void Parse_CompoundAndOrPrecedence()
    {
        // a > 1 AND b < 2 OR c == 'foo'
        // Parsed as: (a > 1 AND b < 2) OR (c == 'foo')
        var ast = ExpressionParser.Parse("a > 1 AND b < 2 OR c == 'foo'");
        ast.Should().BeOfType<OrExpression>();
        var or = (OrExpression)ast;
        or.Operands.Should().HaveCount(2);
        or.Operands[0].Should().BeOfType<AndExpression>();
        or.Operands[1].Should().BeOfType<ComparisonExpression>();
    }

    [Fact]
    public void Parse_NotExpression()
    {
        var ast = ExpressionParser.Parse("NOT gpu.has_data");
        ast.Should().BeOfType<NotExpression>();
        var not = (NotExpression)ast;
        not.Operand.Should().BeOfType<FieldRefNode>().Which.FieldPath.Should().Be("gpu.has_data");
    }

    [Fact]
    public void Parse_BareFieldRef()
    {
        var ast = ExpressionParser.Parse("frametime.has_data");
        ast.Should().BeOfType<FieldRefNode>().Which.FieldPath.Should().Be("frametime.has_data");
    }

    [Fact]
    public void Parse_StringComparison()
    {
        var ast = ExpressionParser.Parse("system.power_plan == 'Balanced'");
        ast.Should().BeOfType<ComparisonExpression>();
        var cmp = (ComparisonExpression)ast;
        cmp.Right.Should().BeOfType<StringLiteralNode>().Which.Value.Should().Be("Balanced");
    }

    [Fact]
    public void Parse_BoolLiteral()
    {
        var ast = ExpressionParser.Parse("disk.os_drive_is_hdd == true");
        ast.Should().BeOfType<ComparisonExpression>();
        var cmp = (ComparisonExpression)ast;
        cmp.Right.Should().BeOfType<BoolLiteralNode>().Which.Value.Should().BeTrue();
    }

    [Fact]
    public void Parse_MultipleAnd()
    {
        var ast = ExpressionParser.Parse("a > 1 AND b > 2 AND c > 3");
        ast.Should().BeOfType<AndExpression>();
        ((AndExpression)ast).Operands.Should().HaveCount(3);
    }

    [Fact]
    public void Parse_AllComparisonOperators()
    {
        ExpressionParser.Parse("a > 1").Should().BeOfType<ComparisonExpression>();
        ExpressionParser.Parse("a < 1").Should().BeOfType<ComparisonExpression>();
        ExpressionParser.Parse("a >= 1").Should().BeOfType<ComparisonExpression>();
        ExpressionParser.Parse("a <= 1").Should().BeOfType<ComparisonExpression>();
        ExpressionParser.Parse("a == 1").Should().BeOfType<ComparisonExpression>();
        ExpressionParser.Parse("a != 1").Should().BeOfType<ComparisonExpression>();
    }

    [Fact]
    public void ParseError_DoubleGreaterThan()
    {
        var act = () => ExpressionParser.Parse("a >> 5");
        act.Should().Throw<ExpressionParseException>()
            .Where(e => e.Position >= 2);
    }

    [Fact]
    public void ParseError_UnexpectedEndOfExpression()
    {
        var act = () => ExpressionParser.Parse("a > ");
        act.Should().Throw<ExpressionParseException>()
            .WithMessage("*end of expression*");
    }

    [Fact]
    public void ParseError_EmptyExpression()
    {
        var act = () => ExpressionParser.Parse("");
        act.Should().Throw<ExpressionParseException>();
    }

    [Fact]
    public void ParseError_UnterminatedString()
    {
        var act = () => ExpressionParser.Parse("a == 'unterminated");
        act.Should().Throw<ExpressionParseException>()
            .WithMessage("*Unterminated*");
    }

    [Fact]
    public void GetFieldReferences_FindsAll()
    {
        var ast = ExpressionParser.Parse("cpu.load > 90 AND memory.used > 80 OR gpu.temp > 85");
        var fields = ExpressionParser.GetFieldReferences(ast);
        fields.Should().BeEquivalentTo(new[] { "cpu.load", "memory.used", "gpu.temp" });
    }

    [Fact]
    public void Parse_DecimalNumber()
    {
        var ast = ExpressionParser.Parse("frametime.p99_ms > 33.3");
        var cmp = (ComparisonExpression)ast;
        cmp.Right.Should().BeOfType<NumberLiteralNode>().Which.Value.Should().Be(33.3);
    }

    [Fact]
    public void Parse_NotEqualString()
    {
        var ast = ExpressionParser.Parse("system.power_plan != 'High performance'");
        var cmp = (ComparisonExpression)ast;
        cmp.Operator.Should().Be(TokenType.NotEqual);
        cmp.Right.Should().BeOfType<StringLiteralNode>().Which.Value.Should().Be("High performance");
    }
}
