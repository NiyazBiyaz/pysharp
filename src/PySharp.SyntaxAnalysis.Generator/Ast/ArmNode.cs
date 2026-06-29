using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Generator.Ast;

internal record ArmNode : GreenNode
{
    internal AlternativeNode Alternative => (AlternativeNode)Children![1];
}
