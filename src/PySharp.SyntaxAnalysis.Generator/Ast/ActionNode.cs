using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Generator.Ast;

internal abstract record ActionNode : GreenNode
{
    internal NodeArray<TargetNode> Arguments => ((NodeList)Children![3]).GetArray<TargetNode>(); // TODO: Add JoinedList primitive.
}

internal record NamedActionNode : ActionNode
{
    internal TokenNode Name => (TokenNode)Children![1];
}

internal record InferredActionNode : ActionNode;
