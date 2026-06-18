namespace PySharp.SyntaxAnalysis.Generator.Ast;

internal record NameAtomNode : AtomNode
{
    public string Value { get; private init; }

    public NameAtomNode(string name)
    {
        Value = name;
    }

    public override string ToString() => $"NameAtomNode({Value})";
}
