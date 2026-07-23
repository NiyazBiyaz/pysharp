using System.Diagnostics;
using PySharp.SyntaxAnalysis.Tokens;

namespace PySharp.SyntaxAnalysis.Common.Ast;

public sealed class TokenView : RedView
{
    private TokenNode token => (TokenNode)Green;

    public ViewArray<TokenView> LeadingTrivia { get; }

    public int Width => token.Width;

    public int FullWidth => token.FullWidth;

    public TokenView(TokenNode node, int position, IRedView? parent)
        : base(node, position, parent)
    {
        Debug.Assert(!parent?.IsArray ?? true, "Arrays cannot be used as parent.");

        LeadingTrivia = new(token.Leading, FullPosition, this);
    }

    public TokenType Type => token.Type;

    public string RawString => token.RawString;
}
