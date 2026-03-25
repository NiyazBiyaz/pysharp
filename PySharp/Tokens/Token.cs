using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Tokens;

[DebuggerDisplay("{Type} {Lexeme} {Start} {End}")]
public readonly record struct Token
{
    public required TokenType Type { get; init; }
    public required ReadOnlyMemory<char> Lexeme { get; init; }
    public required TokenPosition Start { get; init; }
    public required TokenPosition End { get; init; }

    [SetsRequiredMembers]
    public Token(TokenType type, ReadOnlyMemory<char> lexeme, TokenPosition start, TokenPosition end)
    {
        Type = type;
        Lexeme = lexeme;
        Start = start;
        End = end;

        // Assert that, if token placed on single line, lexeme length is equal to columns difference.
        Debug.Assert(Start.Line != End.Line || Lexeme.Length == End.Column - Start.Column);
    }

    [SetsRequiredMembers]
    public Token(TokenType type, string lexeme, TokenPosition start, TokenPosition end)
        : this(type, lexeme.AsMemory(), start, end)
    {
    }
}
