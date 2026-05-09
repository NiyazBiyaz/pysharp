namespace PySharp.SyntaxAnalysis.Generator.Ast;

internal record RepeatZeroMoreNode : RepeatMoleculeNode
{
    public override int MinCount => 0;

    public RepeatZeroMoreNode(AtomNode value)
    {
        Atom = value;
    }
}
