using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Generator.Ast;

internal record MetadataNode(string Name, string StringValue) : GreenNode
{
    public override string ToString() => $"MetadataNode({Name})";
}
