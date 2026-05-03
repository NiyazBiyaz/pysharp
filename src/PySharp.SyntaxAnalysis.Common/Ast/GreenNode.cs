using System.Text;
using PySharp.SyntaxAnalysis.Tokens;

namespace PySharp.SyntaxAnalysis.Common.Ast;

public abstract record GreenNode
{
    public virtual TokenPosition FullOffset2D
    {
        get;
        protected init
        {
            field = default;
            if (Children is not null)
            {
                foreach (var child in Children)
                    field += child.FullOffset2D;
            }
        }
    }
    public virtual INodeArray<GreenNode>? Children { get; init; }

    public virtual string RecoverText()
    {
        if (Children is null)
            return string.Empty;

        var builder = new StringBuilder();
        foreach (var child in Children)
            child.AppendToBuilder(builder);

        return builder.ToString();
    }

    protected virtual void AppendToBuilder(StringBuilder builder)
    {
        if (Children is null)
            return;

        foreach (var child in Children)
            child.AppendToBuilder(builder);
    }
}
