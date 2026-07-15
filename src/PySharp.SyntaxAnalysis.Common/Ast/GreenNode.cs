using System.Text;
using PySharp.SyntaxAnalysis.Tokens;

namespace PySharp.SyntaxAnalysis.Common.Ast;

public abstract record GreenNode : IGreenNode
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

    public virtual INodeArray<IGreenNode>? Children { get; init; }

    public virtual string RecoverText()
    {
        if (Children is null)
            return string.Empty;

        var builder = new StringBuilder();
        foreach (var child in Children)
            child.AppendToBuilder(builder);

        return builder.ToString();
    }

    public virtual void AppendToBuilder(StringBuilder builder)
    {
        if (Children is null)
            return;

        foreach (var child in Children)
            child.AppendToBuilder(builder);
    }
}
