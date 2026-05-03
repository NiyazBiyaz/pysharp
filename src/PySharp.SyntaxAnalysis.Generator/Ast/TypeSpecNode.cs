using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Generator.Ast;

internal record TypeSpecNode : GreenNode
{
    public string TypeName { get; }

    public TypeSpecNode(string typeName)
    {
        TypeName = typeName;
    }
}
