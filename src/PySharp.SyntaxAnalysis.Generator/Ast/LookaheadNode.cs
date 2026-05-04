namespace PySharp.SyntaxAnalysis.Generator.Ast;

internal record LookaheadNode : MoleculeNode
{
    public bool Positiveness { get; private init; }

    public LookaheadNode(AtomNode inner, bool positive)
    {
        Atom = inner;
        Positiveness = positive;
    }
}
