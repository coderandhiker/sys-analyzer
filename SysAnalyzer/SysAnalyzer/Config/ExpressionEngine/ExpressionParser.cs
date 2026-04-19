namespace SysAnalyzer.Config.ExpressionEngine;

/// <summary>
/// PEG parser for trigger expressions (§6.3).
/// Grammar:
///   Expression  := OrExpr
///   OrExpr      := AndExpr ('OR' AndExpr)*
///   AndExpr     := NotExpr ('AND' NotExpr)*
///   NotExpr     := 'NOT' Comparison | Comparison
///   Comparison  := Value CompOp Value | Value
///   CompOp      := '>' | '&lt;' | '>=' | '&lt;=' | '==' | '!='
///   Value       := FieldRef | NumberLiteral | StringLiteral | BoolLiteral
/// </summary>
public sealed class ExpressionParser
{
    private readonly List<Token> _tokens;
    private readonly string _expression;
    private int _pos;

    private ExpressionParser(List<Token> tokens, string expression)
    {
        _tokens = tokens;
        _expression = expression;
        _pos = 0;
    }

    public static ExpressionNode Parse(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            throw new ExpressionParseException("Empty expression", 0, expression ?? "");

        var tokenizer = new Tokenizer(expression);
        var tokens = tokenizer.Tokenize();
        var parser = new ExpressionParser(tokens, expression);
        var result = parser.ParseOrExpr();

        if (parser.Current.Type != TokenType.EndOfExpression)
            throw new ExpressionParseException(
                $"Unexpected token '{parser.Current.Value}' at position {parser.Current.Position}",
                parser.Current.Position, expression);

        return result;
    }

    /// <summary>
    /// Collect all field references in the expression for validation.
    /// </summary>
    public static IReadOnlyList<string> GetFieldReferences(ExpressionNode node)
    {
        var fields = new List<string>();
        CollectFields(node, fields);
        return fields;
    }

    private static void CollectFields(ExpressionNode node, List<string> fields)
    {
        switch (node)
        {
            case FieldRefNode f:
                fields.Add(f.FieldPath);
                break;
            case OrExpression or:
                foreach (var op in or.Operands) CollectFields(op, fields);
                break;
            case AndExpression and:
                foreach (var op in and.Operands) CollectFields(op, fields);
                break;
            case NotExpression not:
                CollectFields(not.Operand, fields);
                break;
            case ComparisonExpression cmp:
                CollectFields(cmp.Left, fields);
                CollectFields(cmp.Right, fields);
                break;
        }
    }

    private Token Current => _tokens[_pos];

    private Token Advance()
    {
        var token = _tokens[_pos];
        _pos++;
        return token;
    }

    private ExpressionNode ParseOrExpr()
    {
        var operands = new List<ExpressionNode> { ParseAndExpr() };
        while (Current.Type == TokenType.Or)
        {
            Advance(); // consume OR
            operands.Add(ParseAndExpr());
        }
        return operands.Count == 1 ? operands[0] : new OrExpression(operands);
    }

    private ExpressionNode ParseAndExpr()
    {
        var operands = new List<ExpressionNode> { ParseNotExpr() };
        while (Current.Type == TokenType.And)
        {
            Advance(); // consume AND
            operands.Add(ParseNotExpr());
        }
        return operands.Count == 1 ? operands[0] : new AndExpression(operands);
    }

    private ExpressionNode ParseNotExpr()
    {
        if (Current.Type == TokenType.Not)
        {
            Advance(); // consume NOT
            return new NotExpression(ParseComparison());
        }
        return ParseComparison();
    }

    private ExpressionNode ParseComparison()
    {
        var left = ParseValue();

        if (IsComparisonOp(Current.Type))
        {
            var op = Advance();
            var right = ParseValue();
            return new ComparisonExpression(left, op.Type, right);
        }

        return left; // bare value (truthy check)
    }

    private ExpressionNode ParseValue()
    {
        var token = Current;
        return token.Type switch
        {
            TokenType.FieldRef => ParseFieldRef(),
            TokenType.NumberLiteral => ParseNumber(),
            TokenType.StringLiteral => ParseString(),
            TokenType.BoolLiteral => ParseBool(),
            TokenType.EndOfExpression => throw new ExpressionParseException(
                "Unexpected end of expression", token.Position, _expression),
            _ => throw new ExpressionParseException(
                $"Expected a value (field, number, string, or boolean) but found '{token.Value}'",
                token.Position, _expression)
        };
    }

    private ExpressionNode ParseFieldRef()
    {
        var token = Advance();
        return new FieldRefNode(token.Value);
    }

    private ExpressionNode ParseNumber()
    {
        var token = Advance();
        if (!double.TryParse(token.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value))
            throw new ExpressionParseException($"Invalid number '{token.Value}'", token.Position, _expression);
        return new NumberLiteralNode(value);
    }

    private ExpressionNode ParseString()
    {
        var token = Advance();
        return new StringLiteralNode(token.Value);
    }

    private ExpressionNode ParseBool()
    {
        var token = Advance();
        return new BoolLiteralNode(token.Value == "true");
    }

    private static bool IsComparisonOp(TokenType type) => type is
        TokenType.GreaterThan or TokenType.LessThan or
        TokenType.GreaterThanOrEqual or TokenType.LessThanOrEqual or
        TokenType.Equal or TokenType.NotEqual;
}
