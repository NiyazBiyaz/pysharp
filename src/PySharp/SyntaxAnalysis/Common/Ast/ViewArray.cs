using System.Collections;

namespace PySharp.SyntaxAnalysis.Common.Ast;

public readonly struct ViewArray<TView>(INodeArray<IGreenNode> greens, int position, IRedView? parent) : IViewArray<TView>
    where TView : IRedView
{
    private readonly TView?[] views = new TView[greens.Count];

    private readonly INodeArray<IGreenNode> greens = greens;

    public TView this[int index] => ensureViewOnIndex(index);

    public bool IsArray => true;

    public IRedView? Parent { get; } = parent;

    public int FullPosition { get; } = position;

    public int Count => views.Length;

    public SyntaxViewTree SyntaxTree => throw new NotSupportedException($"Using syntax tree for the {nameof(ViewArray<>)} is not allowed.");

    public Position2D FullPosition2D => throw new NotImplementedException();

    public int Position => throw new NotImplementedException();

    public int EndPosition => throw new NotImplementedException();

    public Position2D Position2D => throw new NotImplementedException();

    public Position2D EndPosition2D => throw new NotImplementedException();

    private TView ensureViewOnIndex(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index, nameof(index));
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, views.Length, nameof(index));

        if (views[index] == null)
        {
            int position = FullPosition;
            for (int indexBeforeChild = 0; indexBeforeChild < index; indexBeforeChild++)
            {
                position += greens[indexBeforeChild].FullWidth;
            }

            views[index] = (TView)greens[index].GetView(position, Parent);
        }

        return views[index]!;
    }

    private void ensureViews()
    {
        int positionAccumulator = FullPosition;
        for (int index = 0; index < views.Length; index++)
        {
            if (views[index] == null)
            {
                views[index] = (TView)greens[index].GetView(positionAccumulator, Parent);
            }
            positionAccumulator += greens[index].FullWidth;
        }
    }

    public IEnumerator<TView> GetEnumerator()
    {
        ensureViews();
        return ((IEnumerable<TView>)views!).GetEnumerator();
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public string PrettyPrint() => greens.PrettyPrint();

    public string RecoverText() => greens.RecoverText();
}
