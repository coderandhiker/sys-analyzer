namespace SysAnalyzer.Config.ExpressionEngine;

public class ExpressionParseException : Exception
{
    public int Position { get; }
    public string Expression { get; }

    public ExpressionParseException(string message, int position, string expression)
        : base(FormatMessage(message, position, expression))
    {
        Position = position;
        Expression = expression;
    }

    private static string FormatMessage(string message, int position, string expression)
    {
        var pointer = new string(' ', Math.Min(position, expression.Length)) + "^";
        return $"{message}\n  \"{expression}\"\n   {pointer}";
    }
}

public class ExpressionEvaluationException : Exception
{
    public ExpressionEvaluationException(string message) : base(message) { }
}
