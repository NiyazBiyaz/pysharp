using System.Collections;
using PySharp.SyntaxAnalysis.Tokens;

namespace PySharp.SyntaxAnalysis.Common.Ast;

public readonly struct ViewArray<TView>(INodeArray<IGreenNode> greens, TokenPosition position, IRedView? parent) : IViewArray<TView>
    where TView : IRedView
{
    private readonly TView?[] views = new TView[greens.Count];

    private readonly INodeArray<IGreenNode> greens = greens;

    public TView this[int index] => ensureViewOnIndex(index);

    public bool IsArray => true;

    public IRedView? Parent { get; } = parent;

    public TokenPosition Position { get; } = position;

    public TokenPosition EndPosition => Position + greens.FullOffset2D;

    public int Count => views.Length;

    private TView ensureViewOnIndex(int index)
    {
        if (views[index] == null)
        {
            var position = Position;
            for (int indexBeforeChild = 0; indexBeforeChild < index - 1; indexBeforeChild++)
            {
                position += greens[indexBeforeChild].FullOffset2D;
            }
            views[index] = (TView)greens[index].GetView(position, Parent);
        }

        return views[index]!;
    }

    private void ensureViews()
    {
        var positionAccumulator = Position;
        for (int index = 0; index < views.Length; index++)
        {
            positionAccumulator += greens[index].FullOffset2D;

            if (views[index] == null)
            {
                views[index] = (TView)greens[index].GetView(positionAccumulator, Parent);
            }
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
