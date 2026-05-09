namespace PySharp.SyntaxAnalysis.Generator.Ast;

internal record RepeatOneMoreNode : RepeatMoleculeNode
{
    public override int MinCount => 1;

    public RepeatOneMoreNode(AtomNode value)
    {
        Atom = value;
    }
}
