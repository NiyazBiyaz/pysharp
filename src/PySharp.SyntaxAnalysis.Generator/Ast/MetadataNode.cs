using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Generator.Ast;

internal record MetadataNode : GreenNode
{
    internal TokenNode Key => (TokenNode)Children![1];
    internal TokenNode Value => (TokenNode)Children![2];
}
