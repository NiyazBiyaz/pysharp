using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Generator.Ast;

internal record GrammarNode : GreenNode
{
    public NodeArray<MetadataNode> Metadata { get; private init; }
    public NodeArray<AliasNode> Aliases { get; private init; }
    public NodeArray<RuleNode> Rules { get; private init; }

    public GrammarNode(NodeArray<MetadataNode> metadata, NodeArray<AliasNode> aliases, NodeArray<RuleNode> rules)
    {
        Metadata = metadata;
        Aliases = aliases;
        Rules = rules;
    }
}
