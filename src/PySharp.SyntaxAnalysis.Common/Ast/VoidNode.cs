using PySharp.SyntaxAnalysis.Tokens;

namespace PySharp.SyntaxAnalysis.Common.Ast;

/// <summary>
/// Special node type that represents empty node. Used as value if optional node is not match.
/// </summary>
public record VoidNode : GreenNode
{
    public static readonly VoidNode Instance = new();

    public override INodeArray<GreenNode>? Children => null;
    public override TokenPosition FullOffset2D => new(0, 0);
    public override string RecoverText() => "";
}
