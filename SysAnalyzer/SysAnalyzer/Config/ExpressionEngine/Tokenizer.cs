namespace SysAnalyzer.Config.ExpressionEngine;

public enum TokenType
{
    FieldRef,
    NumberLiteral,
    StringLiteral,
    BoolLiteral,
    And,
    Or,
    Not,
    GreaterThan,
    LessThan,
    GreaterThanOrEqual,
    LessThanOrEqual,
    Equal,
    NotEqual,
    EndOfExpression
}

public record Token(TokenType Type, string Value, int Position);

public sealed class Tokenizer
{
    private readonly string _input;
    private int _pos;

    public Tokenizer(string input)
    {
        _input = input;
        _pos = 0;
    }

    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();
        while (_pos < _input.Length)
        {
            SkipWhitespace();
            if (_pos >= _input.Length) break;

            var startPos = _pos;
            var c = _input[_pos];

            if (c == '\'')
            {
                tokens.Add(ReadStringLiteral());
            }
            else if (char.IsDigit(c) || (c == '-' && _pos + 1 < _input.Length && char.IsDigit(_input[_pos + 1])))
            {
                tokens.Add(ReadNumberLiteral());
            }
            else if (c == '>' || c == '<' || c == '=' || c == '!')
            {
                tokens.Add(ReadOperator());
            }
            else if (char.IsLetter(c) || c == '_')
            {
                tokens.Add(ReadIdentifierOrKeyword());
            }
            else
            {
                throw new ExpressionParseException($"Unexpected character '{c}' at position {_pos}", _pos, _input);
            }
        }
        tokens.Add(new Token(TokenType.EndOfExpression, "", _pos));
        return tokens;
    }

    private void SkipWhitespace()
    {
        while (_pos < _input.Length && char.IsWhiteSpace(_input[_pos]))
            _pos++;
    }

    private Token ReadStringLiteral()
    {
        var start = _pos;
        _pos++; // skip opening quote
        var sb = new System.Text.StringBuilder();
        while (_pos < _input.Length && _input[_pos] != '\'')
        {
            sb.Append(_input[_pos]);
            _pos++;
        }
        if (_pos >= _input.Length)
            throw new ExpressionParseException($"Unterminated string literal starting at position {start}", start, _input);
        _pos++; // skip closing quote
        return new Token(TokenType.StringLiteral, sb.ToString(), start);
    }

    private Token ReadNumberLiteral()
    {
        var start = _pos;
        if (_input[_pos] == '-') _pos++;
        while (_pos < _input.Length && char.IsDigit(_input[_pos]))
            _pos++;
        if (_pos < _input.Length && _input[_pos] == '.')
        {
            _pos++;
            while (_pos < _input.Length && char.IsDigit(_input[_pos]))
                _pos++;
        }
        return new Token(TokenType.NumberLiteral, _input[start.._pos], start);
    }

    private Token ReadOperator()
    {
        var start = _pos;
        var c = _input[_pos];
        _pos++;

        if (_pos < _input.Length && _input[_pos] == '=')
        {
            _pos++;
            return c switch
            {
                '>' => new Token(TokenType.GreaterThanOrEqual, ">=", start),
                '<' => new Token(TokenType.LessThanOrEqual, "<=", start),
                '=' => new Token(TokenType.Equal, "==", start),
                '!' => new Token(TokenType.NotEqual, "!=", start),
                _ => throw new ExpressionParseException($"Unknown operator '{c}=' at position {start}", start, _input)
            };
        }

        return c switch
        {
            '>' => new Token(TokenType.GreaterThan, ">", start),
            '<' => new Token(TokenType.LessThan, "<", start),
            _ => throw new ExpressionParseException($"Unknown operator '{c}' at position {start}. Expected: >, <, >=, <=, ==, !=", start, _input)
        };
    }

    private Token ReadIdentifierOrKeyword()
    {
        var start = _pos;
        while (_pos < _input.Length && (char.IsLetterOrDigit(_input[_pos]) || _input[_pos] == '_' || _input[_pos] == '.'))
            _pos++;
        var word = _input[start.._pos];
        var type = word switch
        {
            "AND" => TokenType.And,
            "OR" => TokenType.Or,
            "NOT" => TokenType.Not,
            "true" => TokenType.BoolLiteral,
            "false" => TokenType.BoolLiteral,
            _ => TokenType.FieldRef
        };
        return new Token(type, word, start);
    }
}
