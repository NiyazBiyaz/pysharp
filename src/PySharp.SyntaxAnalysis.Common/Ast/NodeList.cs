namespace PySharp.SyntaxAnalysis.Common.Ast;

public sealed record NodeList : GreenNode
{
    public override INodeArray<GreenNode>? Children => nodes;

    private readonly INodeArray<GreenNode> nodes;

    public NodeList(INodeArray<GreenNode> nodes)
    {
        this.nodes = nodes;
    }
}
