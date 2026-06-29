using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Generator.Ast;

internal record AlternativeNode : GreenNode
{
    internal NodeArray<MoleculeNode> Molecules => ((NodeList)Children![0]).GetArray<MoleculeNode>();
    internal ActionNode? Action => Children![1] as ActionNode;
}
