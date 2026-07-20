using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using PySharp.SyntaxAnalysis.Tokens;

namespace PySharp.SyntaxAnalysis.Common.Ast;

[CollectionBuilder(typeof(NodeArrayBuilder), "Create")]
public class NodeArray<TNode> : INodeArray<TNode>, IEquatable<NodeArray<TNode>>
    where TNode : IGreenNode
{
    private readonly ImmutableArray<TNode> nodes;

    [OverloadResolutionPriority(-1)]
    public NodeArray(IEnumerable<TNode> values)
    {
        nodes = [.. values];
    }

    public NodeArray(ReadOnlySpan<TNode> values)
    {
        nodes = [.. values];
    }

    private int? hashCache = null;

    public TNode this[int index] => nodes[index];

    public int Count => nodes.Length;

    public TokenPosition FullOffset2D
    {
        get
        {
            if (!nodes.IsDefaultOrEmpty && field == default)
            {
                var offsetAccumulator = TokenPosition.StartOfFile;
                foreach (var itemOffset in nodes)
                    offsetAccumulator += itemOffset.FullOffset2D;

                field = offsetAccumulator;
            }

            return field;
        }
    }

    public IRedView GetView(TokenPosition position, IRedView? parent)
        // Creating array requires type parameter, but it breaks contract for non-array nodes.
        => throw new NotSupportedException("NodeArray cannot create it's view. Create ViewArray<T> using it's constructor instead.");

    public bool IsArray => true;

    public INodeArray<IGreenNode>? Children
    {
        get => (INodeArray<IGreenNode>)this;
        init => throw new UnreachableException("Children for the node array should never be initialized. Use regular constructor instead.");
    }

    public bool Equals(NodeArray<TNode>? other)
    {
        if (other is null || Count != other.Count)
            return false;

        for (int i = 0; i < Count; i++)
        {
            if (this[i].Equals(other[i]))
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

    public string PrettyPrint()
    {
        var builder = new StringBuilder();
        AcceptPrettyPrint(builder, 0);
        return builder.ToString();
    }

    public string RecoverText()
    {
        var builder = new StringBuilder();
        foreach (var node in nodes)
            builder.Append(node.RecoverText());

        return builder.ToString();
    }

    public void AcceptPrettyPrint(StringBuilder builder, int indentation)
    {
        builder.Append("NodeArray()");

        if (Children != null)
        {
            builder.AppendLine(" [");
            foreach (var child in Children)
            {
                GreenNode.AddIndentation(builder, indentation + 1);
                child.AcceptPrettyPrint(builder, indentation + 1);
                builder.AppendLine(",");
            }
            GreenNode.AddIndentation(builder, indentation);
            builder.Append(']');
        }
        else
        {
            builder.Append(" []");
        }
    }

    public void AcceptRecoverText(StringBuilder builder)
    {
        foreach (var node in nodes)
        {
            node.AcceptRecoverText(builder);
        }
    }

    public bool Equals(INodeArray<IGreenNode>? other) => other is NodeArray<IGreenNode> otherArray && Equals(otherArray);
}

public static class NodeArrayBuilder
{
    public static NodeArray<TNode> Create<TNode>(ReadOnlySpan<TNode> values)
        where TNode : IGreenNode => new(values);
}
