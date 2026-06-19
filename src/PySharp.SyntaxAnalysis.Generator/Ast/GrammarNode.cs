using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Generator.Ast;

internal record GrammarNode(NodeArray<MetadataNode> Metadata, NodeArray<RuleNode> Rules) : GreenNode;
