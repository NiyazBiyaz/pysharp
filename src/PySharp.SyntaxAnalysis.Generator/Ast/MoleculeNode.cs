using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Generator.Ast;

internal abstract record MoleculeNode : GreenNode;

internal record AtomMoleculeNode : MoleculeNode
{
    internal AtomNode Atom => (AtomNode)Children![0];
}

internal abstract record LookaheadNode : MoleculeNode
{
    internal AtomNode Atom => (AtomNode)Children![1];
}
internal record PositiveLookaheadNode : LookaheadNode;
internal record NegativeLookaheadNode : LookaheadNode;

internal record OptionalNode : MoleculeNode
{
    internal AtomNode Atom => (AtomNode)Children![1];
}

internal abstract record RepeatMoleculeNode : MoleculeNode
{
    internal AtomNode Atom => (AtomNode)Children![0];
}
internal record RepeatOneMoreNode : RepeatMoleculeNode;
internal record RepeatZeroMoreNode : RepeatMoleculeNode;

internal record GatherNode : MoleculeNode
{
    internal AtomNode ValueAtom => (AtomNode)Children![0];
    internal AtomNode Separator => (AtomNode)Children![3];
}
