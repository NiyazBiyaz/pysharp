using System.Collections.Immutable;
using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Generator.Ast;

internal abstract record ActionNode : GreenNode
{
    internal NodeArray<GreenNode> Arguments => ((NodeList)Children![3]).GetArray<GreenNode>();
    private ImmutableArray<TargetNode>? valueArguments
    {
        get
        {
            field ??= Arguments.Where(static (_, i) => i % 2 == 0).Cast<TargetNode>().ToImmutableArray();
            return field;
        }
    }
    internal ImmutableArray<TargetNode> ValueArguments => valueArguments!.Value;
}

internal record NamedActionNode : ActionNode
{
    internal TokenNode Name => (TokenNode)Children![1];
}

internal record InferredActionNode : ActionNode;
