using System.Collections.Immutable;
using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Generator;

internal interface IGroup : IGreenNode
{
    ImmutableArray<AlternativeNode> Alternatives { get; }
    NodeArray<GreenNode> AstAlternatives { get; }
}

internal partial record GroupAtomNode : IGroup
{
    ImmutableArray<AlternativeNode> IGroup.Alternatives => Alternatives;

    NodeArray<GreenNode> IGroup.AstAlternatives => AstAlternatives;
}

internal partial record OptionalGroupNode : IGroup
{
    ImmutableArray<AlternativeNode> IGroup.Alternatives => Alternatives;

    NodeArray<GreenNode> IGroup.AstAlternatives => AstAlternatives;
}
