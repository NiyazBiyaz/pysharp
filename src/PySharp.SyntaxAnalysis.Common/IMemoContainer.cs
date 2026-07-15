using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Common;

public interface IMemoContainer<TNode>
    where TNode : IGreenNode
{
    void AddCache(int tokenPosition, TNode? cache);
    void UpdateCache(int tokenPosition, TNode? cache);
    bool TryGetCache(int tokenPosition, out TNode? cache);
}
