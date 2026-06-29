using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Generator.Ast;

internal record DecoratorNode : GreenNode
{
    internal TokenNode Value => (TokenNode)Children![1];
}
