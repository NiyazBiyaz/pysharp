using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Generator.Ast;

public record AlternativeNode : GreenNode
{
    public ActionNode? Action { get; private init; }
    public NodeArray<AtomNode> Atoms { get; private init; }

    public AlternativeNode(NodeArray<AtomNode> atoms, ActionNode? action)
    {
        Action = action;
        Atoms = atoms;
    }

    public override string ToString() => $"AlternativeNode({Atoms}, {Action})";
}
