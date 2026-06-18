using System.Collections.Immutable;
using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Generator.Ast;

internal record RuleNode : GreenNode
{
    public string Name { get; private init; }
    public ImmutableArray<string> Decorators { get; private init; }
    public TypeSpecNode? TypeSpec { get; private init; }
    public NodeArray<AlternativeNode> Alternatives { get; private init; }

    public RuleNode(ImmutableArray<string> decorators, string name, TypeSpecNode? typeSpec, NodeArray<AlternativeNode> alternatives)
    {
        Decorators = decorators;
        Name = name;
        TypeSpec = typeSpec;
        Alternatives = alternatives;
    }

    public override string ToString() => $"RuleNode({Name}, {Alternatives})";
}
