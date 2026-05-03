using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Generator.Ast;

internal record MetadataNode : GreenNode
{
    public string Name { get; private init; }
    public string StringValue { get; private init; }

    public MetadataNode(string name, string value)
    {
        Name = name;
        StringValue = value;
    }

    public override string ToString() => $"MetadataNode({Name})";
}
