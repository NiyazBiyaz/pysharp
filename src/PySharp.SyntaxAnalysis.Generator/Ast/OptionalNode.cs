namespace PySharp.SyntaxAnalysis.Generator.Ast;

internal record OptionalNode : MoleculeNode
{
    public OptionalNode(AtomNode inner)
    {
        Atom = inner;
    }
}
