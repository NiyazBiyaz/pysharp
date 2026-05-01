using System.Diagnostics;

namespace PySharp.SyntaxAnalysis.Tokens;

[DebuggerDisplay("({Line},{Column})")]
public readonly record struct TokenPosition(int Line, int Column)
{
    public static readonly TokenPosition StartOfFile = new(0, 0);

    public static bool operator <(TokenPosition left, TokenPosition right) =>
        left.Line < right.Line || left.Line == right.Line && left.Column < right.Column;
    public static bool operator >(TokenPosition left, TokenPosition right) =>
        left.Line > right.Line || left.Line == right.Line && left.Column > right.Column;

    public static TokenPosition operator +(TokenPosition left, TokenPosition right) => left.AddDelta(right);
    public static TokenPosition operator -(TokenPosition left, TokenPosition right)
    {
        if (left < right)
            throw new ArgumentException("TokenPosition cannot be negative, but right operand is greater than left.");

        return left.Delta(right);
    }

    public readonly TokenPosition Delta(TokenPosition other)
    {
        if (this == other)
            return StartOfFile;

        if (Line == other.Line)
        {
            return new()
            {
                Line = 0,
                Column = int.Abs(Column - other.Column),
            };
        }
        else
        {
            return new()
            {
                Line = int.Abs(Line - other.Line),
                Column = Line > other.Line ? Column : other.Column,
            };
        }
    }

    public readonly TokenPosition AddDelta(TokenPosition other)
    {
        if (other == StartOfFile)
            return this;
        if (this == StartOfFile)
            return other;

        if (other.Line == 0)
        {
            return new()
            {
                Line = Line,
                Column = Column + other.Column,
            };
        }
        else
        {
            return new()
            {
                Line = Line + other.Line,
                Column = other.Column,
            };
        }
    }
}
