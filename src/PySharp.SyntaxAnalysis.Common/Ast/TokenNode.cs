using PySharp.SyntaxAnalysis.Tokens;

namespace PySharp.SyntaxAnalysis.Common.Ast;

public record TokenNode : GreenNode
{
    public override NodeArray<GreenNode>? Children => null;
    public override TokenPosition FullOffset2D { get; protected init; }
    public TokenPosition Offset2D { get; }
    public TokenType Type { get; }
    public NodeArray<TokenNode> Leading { get; }
    public string RawString { get; }

    public TokenNode(in Token token, IEnumerable<TokenNode> leading)
    {
        Type = token.Type;
        Offset2D = token.End - token.Start;

        TokenPosition acc = TokenPosition.StartOfFile;
        foreach (var node in leading)
            acc += node.Offset2D;
        FullOffset2D = acc + Offset2D;

        Leading = new(leading);
        RawString = token.Lexeme.ToString(); // TODO: add caching.
    }

    public override string ToString() => $"TokenNode({Type}, '{RawString}', {Offset2D})";
}
