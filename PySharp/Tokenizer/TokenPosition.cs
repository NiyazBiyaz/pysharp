using System.Diagnostics;

namespace PySharp.Tokenizer;

[DebuggerDisplay("({Line},{Column})")]
public readonly record struct TokenPosition(int Line, int Column)
{
    public static implicit operator TokenPosition((int line, int column) tuplePos) =>
        new(tuplePos.line, tuplePos.column);
}
