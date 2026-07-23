using System.Diagnostics;

namespace PySharp.SyntaxAnalysis.Common;

[DebuggerDisplay("Position({Line},{Column})")]
public readonly record struct Position2D(int Line, int Column)
{
    public static readonly Position2D StartOfFile = new(0, 0);

    public static bool operator <(Position2D left, Position2D right) =>
        left.Line < right.Line || left.Line == right.Line && left.Column < right.Column;

    public static bool operator >(Position2D left, Position2D right) =>
        left.Line > right.Line || left.Line == right.Line && left.Column > right.Column;

    public static Position2D operator +(Position2D left, Position2D right) => left.AddDelta(right);
    public static Position2D operator -(Position2D left, Position2D right)
    {
        if (left < right)
            throw new ArgumentException("TokenPosition cannot be negative, but right operand is greater than left.");

        return left.Delta(right);
    }

    public readonly Position2D Delta(Position2D other)
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

    public readonly Position2D AddDelta(Position2D other)
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

    public override string ToString() => $"TokenPos({Line}, {Column})";
}
