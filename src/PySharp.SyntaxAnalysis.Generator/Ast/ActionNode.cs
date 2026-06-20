using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Generator.Ast;

// internal record ActionNode(string Expression) : GreenNode;

internal abstract record ActionNode(NodeArray<TargetNode> Arguments) : GreenNode;

internal record NamedActionNode(string Name, NodeArray<TargetNode> Arguments) : ActionNode(Arguments);

internal record InferredActionNode(NodeArray<TargetNode> Arguments) : ActionNode(Arguments);
