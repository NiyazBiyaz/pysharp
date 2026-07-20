using System.Diagnostics;

namespace PySharp.SyntaxAnalysis.Common.Ast;

public abstract class RedView : IRedView
{
    protected readonly IGreenNode Green;

    public int Position { get; }
    public IRedView? Parent { get; }

    public int EndPosition
    {
        get
        {
            if (field == default)
            {
                field = Position + Green.FullWidth;
            }
            return field;
        }
    }

    public virtual bool IsArray => false;

    protected RedView(IGreenNode green, int position, IRedView? parentView)
    {
        Debug.Assert(!parentView?.IsArray ?? true, "Arrays cannot to be used as parent.");

        Green = green;
        Parent = parentView;
        Position = position;
    }

    public int GetPositionFor(int childIndex)
    {
        int position = Position;
        for (int beforeChild = 0; beforeChild < childIndex; beforeChild++)
        {
            position += Green.Children?[beforeChild].FullWidth ?? 0;
        }
        return position;
    }

    public string RecoverText() => Green.RecoverText();

    public string PrettyPrint() => Green.PrettyPrint();
}
