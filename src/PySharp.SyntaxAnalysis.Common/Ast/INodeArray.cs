namespace PySharp.SyntaxAnalysis.Common.Ast;

public interface INodeArray<out TNode> : IReadOnlyList<TNode>, IEquatable<INodeArray<IGreenNode>>
    where TNode : IGreenNode;
