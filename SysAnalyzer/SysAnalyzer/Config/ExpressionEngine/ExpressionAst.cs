namespace SysAnalyzer.Config.ExpressionEngine;

/// <summary>
/// AST node types for the expression tree.
/// Grammar: Expression → OrExpr → AndExpr → NotExpr → Comparison → Value
/// </summary>
public abstract record ExpressionNode;

public record OrExpression(IReadOnlyList<ExpressionNode> Operands) : ExpressionNode;
public record AndExpression(IReadOnlyList<ExpressionNode> Operands) : ExpressionNode;
public record NotExpression(ExpressionNode Operand) : ExpressionNode;
public record ComparisonExpression(ExpressionNode Left, TokenType Operator, ExpressionNode Right) : ExpressionNode;
public record FieldRefNode(string FieldPath) : ExpressionNode;
public record NumberLiteralNode(double Value) : ExpressionNode;
public record StringLiteralNode(string Value) : ExpressionNode;
public record BoolLiteralNode(bool Value) : ExpressionNode;
