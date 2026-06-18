using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Generator.Ast;

internal record GroupAtomNode : AtomNode
{
    public NodeArray<AlternativeNode> Alternatives { get; private init; }

    public GroupAtomNode(NodeArray<AlternativeNode> alts)
    {
        Alternatives = alts;
    }
}
