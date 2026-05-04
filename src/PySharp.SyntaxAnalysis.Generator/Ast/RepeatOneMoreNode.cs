namespace PySharp.SyntaxAnalysis.Generator.Ast;

internal record RepeatOneMoreNode : RepeatMoleculeNode
{
    public RepeatOneMoreNode(AtomNode value)
    {
        Atom = value;
    }
}
