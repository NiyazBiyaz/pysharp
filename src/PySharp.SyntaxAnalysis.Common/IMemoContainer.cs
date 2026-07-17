using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Common;

public interface IMemoContainer<TNode>
    where TNode : IGreenNode
{
    void AddCache(int tokenPosition, int memoEndPosition, TNode? cache);
    void UpdateCache(int tokenPosition, int memoEndPosition, TNode? cache);
    bool TryGetCache(int tokenPosition, out MemoEntry<TNode> cache);
}
