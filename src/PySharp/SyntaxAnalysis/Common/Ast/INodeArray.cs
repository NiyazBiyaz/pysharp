namespace PySharp.SyntaxAnalysis.Common.Ast;

public interface INodeArray<out TNode> : IGreenNode, IReadOnlyList<TNode>
    where TNode : IGreenNode;
