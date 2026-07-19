using System.Text;
using PySharp.SyntaxAnalysis.Tokens;

namespace PySharp.SyntaxAnalysis.Common.Ast;

public interface IGreenNode
{
    TokenPosition FullOffset2D { get; }
    INodeArray<IGreenNode>? Children { get; init; }

    bool IsArray { get; }

    void AcceptRecoverText(StringBuilder builder);

    string RecoverText();

    void AcceptPrettyPrint(StringBuilder builder, int indentation);

    string PrettyPrint();

    IRedView GetView(TokenPosition position, IRedView? parent);
}
