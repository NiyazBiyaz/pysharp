namespace PySharp.SyntaxAnalysis.Generator.Ast;

// I've consider to name it as "Hydrogen", but it's as nice as confusing.
internal sealed record AtomMoleculeNode : MoleculeNode
{
    public AtomMoleculeNode(AtomNode inner)
    {
        Atom = inner;
    }
}
