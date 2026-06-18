using System.Collections;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text;

namespace PySharp.SyntaxAnalysis.Common.Ast;

[CollectionBuilder(typeof(NodeArrayBuilder), "Create")]
public class NodeArray<TNode> : INodeArray<TNode>, IEquatable<NodeArray<TNode>>
    where TNode : GreenNode
{
    private readonly ImmutableArray<TNode> nodes;

    public NodeArray(IEnumerable<TNode> values)
    {
        nodes = [.. values];
    }

    internal NodeArray(ReadOnlySpan<TNode> values)
    {
        nodes = [.. values];
    }

    private int? hashCache = null;

    public TNode this[int index] => nodes[index];

    public int Count => nodes.Length;

    public bool Equals(NodeArray<TNode>? other)
    {
        if (other is null || Count != other.Count)
            return false;

        for (int i = 0; i < Count; i++)
        {
            if (this[i] != other[i])
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is NodeArray<TNode> list && Equals(list);

    public override int GetHashCode()
    {
        if (!hashCache.HasValue)
        {
            var hash = new HashCode();
            foreach (var item in nodes)
                hash.Add(item);
            hashCache = hash.ToHashCode();
        }

        return hashCache.Value;
    }

    public static bool operator ==(NodeArray<TNode> left, NodeArray<TNode> right) => left.Equals(right);
    public static bool operator !=(NodeArray<TNode> left, NodeArray<TNode> right) => !left.Equals(right);

    public IEnumerator<TNode> GetEnumerator() => ((IEnumerable<TNode>)nodes).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString() => $"NodeArray [{string.Join(", ", nodes)}]";

    public string RecoverText()
    {
        var builder = new StringBuilder();
        foreach (var node in nodes)
            builder.Append(node.RecoverText());

        return builder.ToString();
    }
}

public static class NodeArrayBuilder
{
    public static NodeArray<TNode> Create<TNode>(ReadOnlySpan<TNode> values)
        where TNode : GreenNode => new(values);
}
