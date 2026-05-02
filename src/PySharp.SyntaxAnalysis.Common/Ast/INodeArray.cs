namespace PySharp.SyntaxAnalysis.Common.Ast;

public interface INodeArray<out TNode> : IReadOnlyList<TNode>
    where TNode : GreenNode;
