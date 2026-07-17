using System.Text;

namespace PySharp.SyntaxAnalysis.Common.Ast;

public sealed record NodeList : GreenNode
{
    public override INodeArray<IGreenNode>? Children => nodes;

    public override string RecoverText() => nodes.RecoverText();

    private readonly INodeArray<IGreenNode> nodes;

    public NodeList(INodeArray<IGreenNode> nodes)
    {
        this.nodes = nodes;
    }

    public NodeArray<TNode> GetArray<TNode>() where TNode : IGreenNode
        => (NodeArray<TNode>)nodes;

    public override void AcceptPrettyPrint(StringBuilder builder, int indentation)
    {
        builder.Append("NodeList()");

        if (Children != null)
        {
            builder.AppendLine(" [");
            foreach (var child in Children)
            {
                AddIndentation(builder, indentation + 1);
                child.AcceptPrettyPrint(builder, indentation + 1);
                builder.AppendLine(",");
            }
            AddIndentation(builder, indentation);
            builder.Append(']');
        }
        else
        {
            builder.Append(" []");
        }
    }
}
