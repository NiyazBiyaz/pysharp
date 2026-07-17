using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Common;

public readonly record struct MemoEntry<TNode>(int EndPosition, TNode? Cache)
    where TNode : IGreenNode;
