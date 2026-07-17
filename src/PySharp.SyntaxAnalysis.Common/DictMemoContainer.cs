using System.Diagnostics;
using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Common;

public readonly struct DictMemoContainer<TNode> : IMemoContainer<TNode>
    where TNode : IGreenNode
{
    private readonly Dictionary<int, MemoEntry<TNode>> cacheContainer = [];

    public DictMemoContainer()
    {
    }

    public void AddCache(int tokenPosition, int memoEndPosition, TNode? cache)
    {
        Debug.Assert(tokenPosition >= 0);

        if (cacheContainer.ContainsKey(tokenPosition))
            throw new InvalidOperationException("Cache already have this token. Rewriting cache is not allowed.");

        cacheContainer[tokenPosition] = new(memoEndPosition, cache);
    }

    public void UpdateCache(int tokenPosition, int memoEndPosition, TNode? cache)
    {
        Debug.Assert(tokenPosition >= 0);

        cacheContainer[tokenPosition] = new(memoEndPosition, cache);
    }

    public bool TryGetCache(int tokenPosition, out MemoEntry<TNode> cache)
    {
        Debug.Assert(tokenPosition >= 0);

        if (cacheContainer.TryGetValue(tokenPosition, out var _cache))
        {
            cache = _cache;
            return true;
        }

        cache = default;
        return false;
    }
}
