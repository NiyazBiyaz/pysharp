using System.Collections;
using System.Diagnostics;

namespace PySharp.SyntaxAnalysis.Common.Ast;

public readonly struct ViewArray<TView>(INodeArray<IGreenNode> greens, int position, IRedView? parent) : IViewArray<TView>
    where TView : IRedView
{
    private readonly TView?[] views = new TView[greens.Count];

    private readonly INodeArray<IGreenNode> greens = greens;

    public TView this[int index] => ensureViewOnIndex(index);

    public bool IsArray => true;

    public IRedView? Parent { get; } = parent;

    public int Position { get; } = position;

    public int EndPosition => Position + greens.FullWidth;

    public int Count => views.Length;

    private TView ensureViewOnIndex(int index)
    {
        if (views[index] == null)
        {
            int position = Position;
            for (int indexBeforeChild = 0; indexBeforeChild < index; indexBeforeChild++)
            {
                position += greens[indexBeforeChild].FullWidth;
            }
            Debugger.Break();

            views[index] = (TView)greens[index].GetView(position, Parent);
        }

        return views[index]!;
    }

    private void ensureViews()
    {
        int positionAccumulator = Position;
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
