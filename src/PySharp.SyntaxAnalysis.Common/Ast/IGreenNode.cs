using PySharp.SyntaxAnalysis.Tokens;

namespace PySharp.SyntaxAnalysis.Common.Ast;

public interface IGreenNode
{
    TokenPosition FullOffset2D { get; }
    INodeArray<GreenNode>? Children { get; init; }

    string RecoverText();
}
