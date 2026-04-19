namespace SysAnalyzer.Config.ExpressionEngine;

/// <summary>
/// Evaluates an expression AST against a flat dictionary of analysis results.
/// Short-circuit evaluation. Type-strict comparisons. Null → false.
/// </summary>
public sealed class ExpressionEvaluator
{
    private readonly Dictionary<string, object?> _fields;

    public ExpressionEvaluator(Dictionary<string, object?> fields)
    {
        _fields = fields;
    }

    public bool Evaluate(ExpressionNode node)
    {
        return node switch
        {
            OrExpression or => EvaluateOr(or),
            AndExpression and => EvaluateAnd(and),
            NotExpression not => !Evaluate(not.Operand),
            ComparisonExpression cmp => EvaluateComparison(cmp),
            FieldRefNode field => EvaluateTruthy(field),
            BoolLiteralNode b => b.Value,
            NumberLiteralNode n => n.Value > 0,
            StringLiteralNode s => !string.IsNullOrEmpty(s.Value),
            _ => false
        };
    }

    private bool EvaluateOr(OrExpression or)
    {
        foreach (var operand in or.Operands)
        {
            if (Evaluate(operand))
                return true; // short-circuit
        }
        return false;
    }

    private bool EvaluateAnd(AndExpression and)
    {
        foreach (var operand in and.Operands)
        {
            if (!Evaluate(operand))
                return false; // short-circuit
        }
        return true;
    }

    private bool EvaluateTruthy(FieldRefNode field)
    {
        if (!_fields.TryGetValue(field.FieldPath, out var value) || value is null)
            return false;

        return value switch
        {
            bool b => b,
            double d => d > 0,
            int i => i > 0,
            long l => l > 0,
            string s => !string.IsNullOrEmpty(s),
            _ => false
        };
    }

    private bool EvaluateComparison(ComparisonExpression cmp)
    {
        var left = ResolveValue(cmp.Left);
        var right = ResolveValue(cmp.Right);

        // Null handling: any comparison involving null returns false
        if (left is null || right is null)
            return false;

        // Type-strict: both must be same type category
        if (left is double leftNum && right is double rightNum)
            return CompareNumbers(leftNum, rightNum, cmp.Operator);

        if (left is string leftStr && right is string rightStr)
            return CompareStrings(leftStr, rightStr, cmp.Operator);

        if (left is bool leftBool && right is bool rightBool)
            return CompareBools(leftBool, rightBool, cmp.Operator);

        // Type mismatch
        throw new ExpressionEvaluationException(
            $"Type mismatch: cannot compare {left.GetType().Name} with {right.GetType().Name}");
    }

    private object? ResolveValue(ExpressionNode node)
    {
        return node switch
        {
            FieldRefNode field => _fields.TryGetValue(field.FieldPath, out var val) ? NormalizeValue(val) : null,
            NumberLiteralNode n => n.Value,
            StringLiteralNode s => s.Value,
            BoolLiteralNode b => b.Value,
            _ => null
        };
    }

    private static object? NormalizeValue(object? value)
    {
        return value switch
        {
            null => null,
            int i => (double)i,
            long l => (double)l,
            float f => (double)f,
            _ => value
        };
    }

    private static bool CompareNumbers(double left, double right, TokenType op) => op switch
    {
        TokenType.GreaterThan => left > right,
        TokenType.LessThan => left < right,
        TokenType.GreaterThanOrEqual => left >= right,
        TokenType.LessThanOrEqual => left <= right,
        TokenType.Equal => Math.Abs(left - right) < 0.0001,
        TokenType.NotEqual => Math.Abs(left - right) >= 0.0001,
        _ => false
    };

    private static bool CompareStrings(string left, string right, TokenType op) => op switch
    {
        TokenType.Equal => string.Equals(left, right, StringComparison.Ordinal),
        TokenType.NotEqual => !string.Equals(left, right, StringComparison.Ordinal),
        _ => throw new ExpressionEvaluationException($"Cannot use operator '{op}' with string values. Only == and != are supported for strings.")
    };

    private static bool CompareBools(bool left, bool right, TokenType op) => op switch
    {
        TokenType.Equal => left == right,
        TokenType.NotEqual => left != right,
        _ => throw new ExpressionEvaluationException($"Cannot use operator '{op}' with boolean values. Only == and != are supported for booleans.")
    };
}
