using System.Diagnostics;

namespace PySharp.SyntaxAnalysis.Common.Ast;

public abstract class RedView : IRedView
{
    protected readonly IGreenNode Green;

    public int FullPosition { get; }
    public IRedView? Parent { get; }

    public int EndPosition
    {
        get
        {
            if (field == default)
            {
                field = FullPosition + Green.FullWidth;
            }
            return field;
        }
    }

    public bool IsArray => false;

    private SyntaxViewTree? syntaxTree = null;

    public SyntaxViewTree SyntaxTree
    {
        get
        {
            // Found nearest parent with the syntax tree and cache it.
            syntaxTree ??= Parent?.SyntaxTree
                ?? throw new NullReferenceException("SyntaxTree for the current view tree is not set.");

            return syntaxTree.Value;
        }
        set => syntaxTree = value;
    }

    public Position2D FullPosition2D => SyntaxTree.PositionMap.GetPosition2D(FullPosition);

    public Position2D Position2D => SyntaxTree.PositionMap.GetPosition2D(Position);

    public Position2D EndPosition2D => SyntaxTree.PositionMap.GetPosition2D(EndPosition);

    public int Position => FullPosition + (Green.TriviaWidth ?? 0);

    protected RedView(IGreenNode green, int position, IRedView? parentView)
    {
        Debug.Assert(!parentView?.IsArray ?? true, "Arrays cannot to be used as parent.");

        Green = green;
        Parent = parentView;
        FullPosition = position;
    }

    public int GetPositionFor(int childIndex)
    {
        int position = FullPosition;
        for (int beforeChild = 0; beforeChild < childIndex; beforeChild++)
        {
            position += Green.Children?[beforeChild].FullWidth ?? 0;
        }
        return position;
    }

    public string RecoverText() => Green.RecoverText();

    public string PrettyPrint() => Green.PrettyPrint();
}
