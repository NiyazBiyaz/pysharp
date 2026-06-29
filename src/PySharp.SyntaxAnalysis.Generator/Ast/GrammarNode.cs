using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Generator.Ast;

internal record GrammarNode : GreenNode
{
    internal NodeArray<MetadataNode> Metadata => ((NodeList)Children![0]).GetArray<MetadataNode>();
    internal NodeArray<RuleNode> Rules => ((NodeList)Children![1]).GetArray<RuleNode>();
}
