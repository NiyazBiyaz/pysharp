using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Generator.Ast;

internal record AlternativeNode : GreenNode
{
    public ActionNode? Action { get; private init; }
    public NodeArray<MoleculeNode> Molecules { get; private init; }

    public AlternativeNode(NodeArray<MoleculeNode> molecules, ActionNode? action)
    {
        Action = action;
        Molecules = molecules;
    }

    public override string ToString() => $"AlternativeNode({Molecules}, {Action})";
}
