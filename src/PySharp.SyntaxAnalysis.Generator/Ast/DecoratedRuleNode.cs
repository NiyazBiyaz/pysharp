using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Generator.Ast;

internal record DecoratedRuleNode : RuleNode
{
    public string Decorator { get; private init; }

    public DecoratedRuleNode(string decorator, string name, TypeSpecNode? typeSpec, NodeArray<AlternativeNode> alts)
        : base(name, typeSpec, alts)
    {
        Decorator = decorator;
    }
}
