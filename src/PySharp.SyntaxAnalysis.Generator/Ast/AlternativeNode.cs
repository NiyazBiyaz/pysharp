using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Generator.Ast;

internal record AlternativeNode(NodeArray<MoleculeNode> Molecules, ActionNode? Action) : GreenNode
{
    public override string ToString() => $"AlternativeNode({Molecules}, {Action})";
}
