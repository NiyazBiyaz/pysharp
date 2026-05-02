using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Generator.Ast;

public record RuleNode : GreenNode
{
    public string Name { get; private init; }
    public NodeArray<AlternativeNode> Alternatives { get; private init; }

    public RuleNode(string name, NodeArray<AlternativeNode> alternatives)
    {
        Name = name;
        Alternatives = alternatives;
    }

    public override string ToString() => $"RuleNode({Name}, {Alternatives})";
}
