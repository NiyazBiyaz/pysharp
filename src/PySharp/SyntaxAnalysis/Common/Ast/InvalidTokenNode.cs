using System.Diagnostics.CodeAnalysis;
using PySharp.SyntaxAnalysis.Tokens;

namespace PySharp.SyntaxAnalysis.Common.Ast;

public record InvalidTokenNode : TokenNode
{
    [SetsRequiredMembers]
    public InvalidTokenNode(
        in Token token,
        IEnumerable<TokenNode> leading,
        string? message,
        TokenizerError error)
    : base(token, leading)
    {
        Error = error;
        ErrorMessage = message;
    }

    public required string? ErrorMessage { get; init; }
    public required TokenizerError Error { get; init; }
}
