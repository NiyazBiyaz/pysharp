using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Generator.Ast;

internal abstract record RuleNode : GreenNode
{
    internal NodeArray<DecoratorNode> Decorators => ((NodeList)Children![0]).GetArray<DecoratorNode>();
    internal TokenNode Name => (TokenNode)Children![1];
}

internal record ArmedRuleNode : RuleNode
{
    internal NodeArray<ArmNode> Arms => ((NodeList)Children![5]).GetArray<ArmNode>();
}

internal record SingleAlternativeRuleNode : RuleNode
{
    internal AlternativeNode Alternative => (AlternativeNode)Children![3];
}
