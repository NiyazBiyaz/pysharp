namespace PySharp.SyntaxAnalysis.Generator.Ast;

internal record RepeatZeroMoreNode : RepeatMoleculeNode
{
    public RepeatZeroMoreNode(AtomNode value)
    {
        Atom = value;
    }
}
