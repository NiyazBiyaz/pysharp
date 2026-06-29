using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Generator.Ast;

internal record TargetNode : GreenNode
{
    internal TokenNode Field => (TokenNode)Children![0];
    internal TokenNode Variable => (TokenNode)Children![2];
}
