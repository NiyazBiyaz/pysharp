namespace PySharp.SyntaxAnalysis.Common.Ast;

/// <summary>
/// Special node type that represents empty node. Used as value if optional node is not match.
/// </summary>
public record VoidNode : GreenNode
{
    public static readonly VoidNode Instance = new();

    public override INodeArray<GreenNode>? Children => null;
    public override int FullWidth => 0;
    public override string RecoverText() => "";

    public override IRedView GetView(int position, IRedView? parent) => new VoidView(this, position, parent);
}
