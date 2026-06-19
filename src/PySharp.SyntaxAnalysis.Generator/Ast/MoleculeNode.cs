using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Generator.Ast;

internal abstract record MoleculeNode(AtomNode Atom) : GreenNode;

// I've consider to name it as "Hydrogen", but it's as nice as confusing.
internal sealed record AtomMoleculeNode(AtomNode Atom) : MoleculeNode(Atom);

internal record LookaheadNode(AtomNode Atom, bool Positiveness) : MoleculeNode(Atom);

internal record OptionalNode(AtomNode Atom) : MoleculeNode(Atom);

internal abstract record RepeatMoleculeNode(AtomNode Atom) : MoleculeNode(Atom)
{
    public abstract int MinCount { get; }
}

internal record RepeatOneMoreNode(AtomNode Atom) : RepeatMoleculeNode(Atom)
{
    public override int MinCount => 1;
}

internal record RepeatZeroMoreNode(AtomNode Atom) : RepeatMoleculeNode(Atom)
{
    public override int MinCount => 0;
}
