using System.Text;
using PySharp.SyntaxAnalysis.Tokens;

namespace PySharp.SyntaxAnalysis.Common.Ast;

public interface IGreenNode
{
    TokenPosition FullOffset2D { get; }
    INodeArray<IGreenNode>? Children { get; init; }

    void AppendToBuilder(StringBuilder builder);

    public string RecoverText();
}
