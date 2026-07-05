using System.Diagnostics.CodeAnalysis;
using PySharp.SyntaxAnalysis.Common.Ast;
using PySharp.SyntaxAnalysis.Tokens;

namespace PySharp.SyntaxAnalysis.Generator;

internal class BoundGrammar
{
    internal string? ParserName { get; set; }
    internal string? TopLevelNodeName { get; set; }
    internal string? UserHeader { get; set; }
    internal List<BoundRule> Rules { get; } = [];
    internal BoundRule? MainRule { get; set; }
    internal List<BoundType> Types { get; } = [];
}

internal class BoundRule
{
    internal required string Name { get; init; }
    internal required BoundRuleKind Kind { get; init; }
    internal required IReadOnlyList<AlternativeNode> AstAlternatives { get; init; }
    internal required string SourceText { get; init; }
    internal virtual required BoundType Type { get; init; }
    internal List<BoundAlternative> Alternatives { get; } = [];
}

internal enum BoundRuleKind
{
    Type,
    Union,
    TokenUnion,
}

internal class BoundAlternative
{
    internal required string SourceText { get; init; }
    internal List<BoundAlternativeEntry> Entries { get; } = [];
    internal IEnumerable<BoundAlternativeEntry> Variables => Entries
        .Where(e => e.Quantifier is not QuantifierKind.Lookahead and not QuantifierKind.Cut);
    internal BoundAction? Action { get; set; }
}

internal class BoundAction
{
    /// <summary>
    /// Type that this action would return if alternative is matched.
    /// </summary>
    internal required BoundRuleType Type { get; init; }
    /// <summary>
    /// Variables that was captured in the <see cref="ActionNode"/> and would be used to generate
    /// type fields.
    /// </summary>
    internal required List<BoundCapturedVariable> CapturedVariables { get; init; }
}

internal class BoundCapturedVariable
{
    /// <summary>
    /// Name of the field specified in the <see cref="ActionNode"/> to be generated in Ast.
    /// </summary>
    internal required string FieldName { get; init; }
    /// <summary>
    /// Entry in the <see cref="BoundAlternative"/> that this captured variable references on.
    /// </summary>
    internal required BoundAlternativeEntry Entry { get; init; }
}

internal abstract class BoundAlternativeEntry
{
    /// <summary>
    /// Name of the variable that saved to <see cref="GreenNode.Children"/> if match.
    /// Used to identify when in grammar used to create named property.
    /// </summary>
    internal required string Name { get; init; }

    /// <summary>
    /// Index of the entry saved to <see cref="GreenNode.Children"/> to be able retrieve it.
    /// </summary>
    internal int Index { get; set; }

    /// <summary>
    /// Kind of the quantifier that was used to this entry. Depends on the value another fields can be set to
    /// <see langword="null"/> or not.
    /// </summary>
    internal required QuantifierKind Quantifier { get; init; }

    /// <summary>
    /// Represents minimum repeat count of the entry if <see cref="QuantifierKind.Repeat"/> is set
    /// in <see cref="Quantifier"/>. <see langword="null"/> if <see cref="Quantifier"/> is another.
    /// </summary>
    internal required int? MinRepeatCount { get; init; }

    /// <summary>
    /// Represents positive or negative kind of lookahead of the entry if <see cref="QuantifierKind.Lookahead"/>
    /// is set in <see cref="Quantifier"/>. <see langword="null"/> if <see cref="Quantifier"/> is another.
    /// </summary>
    internal required bool? Positiveness { get; init; }
}

internal class BoundCutAlternativeEntry : BoundAlternativeEntry
{
    [SetsRequiredMembers]
    internal BoundCutAlternativeEntry()
    {
        Name = "_cut";
        Quantifier = QuantifierKind.Cut;
    }
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

internal abstract class BoundType
{
    internal List<BoundUnionType> UnionMembership { get; } = [];
    internal required string Name { get; init; }

    internal static readonly BoundRuleType TokenNodeType = new()
    {
        Base = null,
        Name = "TokenNode",
    };
}

internal sealed class BoundRuleType : BoundType
{
    internal required BoundRuleType? Base { get; init; }
    internal List<BoundField> Fields { get; set; } = null!;
}

internal sealed class BoundUnionType : BoundType
{
    internal List<BoundType> Members { get; } = [];
}

internal record BoundField
{
    internal required int Index { get; init; }
    internal required AccessModifier AccessModifier { get; init; }
    internal required FieldKind Kind { get; init; }
    internal required string Name { get; init; }
    internal required BoundType Type { get; init; }
    internal required bool IsOptional { get; init; }
}

internal enum FieldKind
{
    Plain,
    Array,
    Gather,
}
