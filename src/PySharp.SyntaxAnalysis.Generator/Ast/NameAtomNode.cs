namespace PySharp.SyntaxAnalysis.Generator.Ast;

internal record NameAtomNode : AtomNode
{
    public NameAtomNode(string name)
    {
        Value = name;
    }

    public override string ToString() => $"NameAtomNode({Value})";
}
