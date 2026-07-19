using System.Diagnostics;
using PySharp.SyntaxAnalysis.Tokens;

namespace PySharp.SyntaxAnalysis.Common.Ast;

public readonly struct TokenView : IRedView
{
    private readonly TokenNode token;

    public TokenPosition Position { get; }

    public IRedView? Parent { get; }

    public ViewArray<TokenView> LeadingTrivia { get; }

    public TokenView(TokenNode node, TokenPosition position, IRedView? parent)
    {
        Debug.Assert(!parent?.IsArray ?? true, "Arrays cannot be used as parent.");

        token = node;
        Position = position;
        Parent = parent;
        LeadingTrivia = new(token.Leading, Position, this);
    }

    public TokenPosition EndPosition => Position + token.FullOffset2D;

    public TokenType Type => token.Type;

    public string RawString => token.RawString;

    public bool IsArray => false;

    public string PrettyPrint() => token.PrettyPrint();
    public string RecoverText() => token.RecoverText();
}
