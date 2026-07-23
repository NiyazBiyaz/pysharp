using System.Text;

namespace PySharp.SyntaxAnalysis.Common.Ast;

public interface IGreenNode
{
    int FullWidth { get; }

    int? TriviaWidth { get; }

    INodeArray<IGreenNode>? Children { get; init; }

    bool IsArray { get; }

    void AcceptRecoverText(StringBuilder builder);

    string RecoverText();

    void AcceptPrettyPrint(StringBuilder builder, int indentation);

    string PrettyPrint();

    IRedView GetView(int position, IRedView? parent);
}
