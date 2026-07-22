using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.SyntaxAnalysis.Tokens;

[DebuggerDisplay("{getDebuggerDisplay()}")]
public readonly record struct Token
{
    public required TokenType Type { get; init; }
    public required ReadOnlyMemory<char> Lexeme { get; init; }

    [SetsRequiredMembers]
    public Token(TokenType type, ReadOnlyMemory<char> lexeme)
    {
        Type = type;
        Lexeme = lexeme;
    }

    [SetsRequiredMembers]
    public Token(TokenType type, string lexeme)
        : this(type, lexeme.AsMemory())
    {
    }

    private string getDebuggerDisplay() => $"{{ {Type} '{Lexeme}':{Lexeme.Length} }}";
}
