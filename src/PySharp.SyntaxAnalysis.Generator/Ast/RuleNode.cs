using System.Collections.Immutable;
using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Generator.Ast;

internal record RuleNode(
    ImmutableArray<string> Decorators,
    string Name, TypeSpecNode? TypeSpec,
    NodeArray<AlternativeNode> Alternatives
) : GreenNode
{
    public override string ToString() => $"RuleNode({Name}, {Alternatives})";
}
