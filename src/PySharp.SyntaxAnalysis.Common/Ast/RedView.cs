using System.Diagnostics;
using PySharp.SyntaxAnalysis.Tokens;

namespace PySharp.SyntaxAnalysis.Common.Ast;

public abstract class RedView : IRedView
{
    protected readonly IGreenNode Green;

    public TokenPosition Position { get; }
    public IRedView? Parent { get; }

    public TokenPosition EndPosition
    {
        get
        {
            if (field == default)
            {
                field = Position + Green.FullOffset2D;
            }
            return field;
        }
    }

    public virtual bool IsArray => false;

    protected RedView(IGreenNode green, TokenPosition position, IRedView? parentView)
    {
        Debug.Assert(!parentView?.IsArray ?? true, "Arrays cannot to be used as parent.");

        Green = green;
        Parent = parentView;
        Position = position;
    }

    public TokenPosition GetPositionFor(int childIndex)
    {
        var position = Position;
        for (int beforeChild = 0; beforeChild < childIndex - 1; beforeChild++)
        {
            position += Green.Children?[beforeChild].FullOffset2D ?? TokenPosition.StartOfFile;
        }
        return position;
    }

    public string RecoverText() => Green.RecoverText();

    public string PrettyPrint() => Green.PrettyPrint();
}
