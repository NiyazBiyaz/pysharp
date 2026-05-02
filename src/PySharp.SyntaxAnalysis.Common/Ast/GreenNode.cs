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
}
