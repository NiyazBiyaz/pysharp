using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Generator.Ast;

internal record ActionNode : GreenNode
{
    public string Expression { get; private init; }

    public ActionNode(string expr)
    {
        Expression = expr;
    }

    public override string ToString() => $"ActionNode('{Expression}')";
}
