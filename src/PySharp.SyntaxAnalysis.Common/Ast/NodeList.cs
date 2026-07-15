namespace PySharp.SyntaxAnalysis.Common.Ast;

public sealed record NodeList : GreenNode
{
    public override INodeArray<IGreenNode>? Children => nodes;

    private readonly INodeArray<IGreenNode> nodes;

    public NodeList(INodeArray<IGreenNode> nodes)
    {
        this.nodes = nodes;
    }

    public NodeArray<TNode> GetArray<TNode>() where TNode : IGreenNode
        => (NodeArray<TNode>)nodes;
}
