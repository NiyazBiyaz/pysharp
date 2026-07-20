using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.SyntaxAnalysis.Tokens;

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
    }

    [SetsRequiredMembers]
    public Token(TokenType type, string lexeme, TokenPosition start, TokenPosition end)
        : this(type, lexeme.AsMemory(), start, end)
    {
    }
}
