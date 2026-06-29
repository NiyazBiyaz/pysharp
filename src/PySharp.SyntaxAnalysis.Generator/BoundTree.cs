using PySharp.SyntaxAnalysis.Generator.Ast;
using PySharp.SyntaxAnalysis.Tokens;

namespace PySharp.SyntaxAnalysis.Generator;

internal class BoundGrammar
{
    internal string? ParserName { get; set; }
    internal string? TopLevelNodeName { get; set; }
    internal string? UserHeader { get; set; }
    internal List<BoundRule> Rules { get; } = [];
    internal BoundRule? MainRule { get; set; }
}

internal class BoundRule
{
    internal required string Name { get; init; }
    internal required IList<AlternativeNode> AstAlternatives { get; init; }
    internal required string SourceText { get; init; }
    internal List<BoundAlternative> Alternatives { get; } = [];

    internal string TypeName => Name + "Node";
}

internal class BoundAlternative
{
    internal required string SourceText { get; init; }
    internal IList<BoundAlternativeEntry> Entries => Variables.Values.ToList();
    internal Dictionary<string, BoundAlternativeEntry> Variables { get; } = [];
    internal BoundAction? Action { get; set; }
}

internal class BoundAction
{
    internal required string TypeHint { get; init; }
    internal required List<BoundCapturedVariable> CapturedVariables { get; init; }
}

internal class BoundCapturedVariable
{
    internal required string VariableName { get; init; }
    internal required string FieldName { get; init; }
    internal required BoundAlternativeEntry Entry { get; init; }
}

internal abstract class BoundAlternativeEntry
{
    internal required string Name { get; init; }
    internal required QuantifierKind Quantifier { get; init; }
    internal required int? MinRepeatCount { get; init; }
    internal required bool? Positiveness { get; init; }
}

internal class BoundRuleAlternativeEntry : BoundAlternativeEntry
{
    internal required BoundRule Value { get; init; }
}

internal class BoundTokenAlternativeEntry : BoundAlternativeEntry
{
    internal required TokenType Value { get; init; }
}

internal class BoundStringAlternativeEntry : BoundAlternativeEntry
{
    internal required string Value { get; init; }
}

internal class BoundGatherAlternativeEntry : BoundAlternativeEntry
{
    internal required BoundAlternativeEntry Value { get; init; }
    internal required BoundAlternativeEntry Separator { get; init; }
}
