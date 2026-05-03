using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Generator.Ast;

internal record GrammarNode : GreenNode
{
    public NodeArray<MetadataNode> Metadata { get; private init; }
    public NodeArray<RuleNode> Rules { get; private init; }

    public GrammarNode(NodeArray<MetadataNode> metadata, NodeArray<RuleNode> rules)
    {
        Metadata = metadata;
        Rules = rules;
    }
}
