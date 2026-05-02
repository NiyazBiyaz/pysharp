namespace PySharp.SyntaxAnalysis.Common.Ast;

public sealed record NodeArrayWrapNode : GreenNode
{
    public override INodeArray<GreenNode>? Children => nodes;

    private readonly INodeArray<GreenNode> nodes;

    public NodeArrayWrapNode(INodeArray<GreenNode> nodes)
    {
        this.nodes = nodes;
    }
}
