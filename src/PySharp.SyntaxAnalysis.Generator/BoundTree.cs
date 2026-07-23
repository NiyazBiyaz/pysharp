using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using PySharp.SyntaxAnalysis.Common.Ast;
using PySharp.SyntaxAnalysis.Tokens;

namespace PySharp.SyntaxAnalysis.Generator;

internal class BoundGrammar
{
    internal string ParserName { get; set; } = null!;
    internal string TopLevelNodeName { get; set; } = null!;
    internal string UserHeader { get; set; } = null!;
    internal BoundRule MainRule { get; set; } = null!;
    internal List<BoundRule> Rules { get; } = [];
    internal List<BoundType> Types { get; } = [];

    internal string GenerateCode()
    {
        var gen = new CsGenerator();

        gen.AddParserSignature(AccessModifier.Internal, ParserName, TopLevelNodeName);

        gen.AddParserBody(MainRule.Name, TopLevelNodeName, Rules.Select(r => r.ToIr()), []);

        gen.AddLine("#region Type definitions");

        gen.AddTypes(Types.Select(t => t.ToIr()));

        gen.AddLine("#endregion");

        return gen.Dump();
    }
}

internal class BoundRule
{
    internal required string Name { get; init; }
    internal required RuleKind Kind { get; init; }
    internal required IReadOnlyList<AlternativeView> AstAlternatives { get; init; }
    internal required string SourceText { get; init; }
    internal required BoundType Type { get; init; }
    internal required int LineCreated { get; init; }
    internal List<BoundAlternative> Alternatives { get; } = [];
    internal required bool IsGroup { get; init; }
    internal required bool EnableMemoization { get; init; }
    internal bool IsEntryPoint { get; set; }
    internal bool IsLeftRecursive { get; set; } = false;
    internal bool WasUsed { get; set; } = false;

    // Override to be able to use in the HashSet<BoundRule> when InspectRules in Binder.
    // Not record because fields like IsLeader can be changed and value forever lost
    // in the HashSet because we do not have copy.
    // It's safe to just use names because grammar does not allow to use multiple rules
    // with the same name and you can't duplicate it somehow.
    public bool Equals(BoundRule? other)
    {
        if (other == null)
            return false;

        if (Name != other.Name)
        {
            return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is BoundRule other && Equals(other);

    public override int GetHashCode()
    {
        HashCode hash = new();

        hash.Add(Name);

        return hash.ToHashCode();
    }

    internal RuleIr ToIr() => new(
        SourceText,
        Name,
        Kind,
        EnableMemoization,
        IsLeftRecursive,
        Alternatives.Select(a => a.ToIr(Kind != RuleKind.Type)));

    internal IEnumerable<BoundRule> GetPotentialLeftRecursive(List<CompilationWarning> warnings) =>
        Alternatives
        .SelectMany(alt => alt.GetPotentialLeftRecursive(warnings))
        .Select(ruleEntry => ruleEntry.Value);

    internal IEnumerable<BoundRule> GetAllUsedRules() =>
        Alternatives
        .SelectMany(alt => alt.GetAllUsedRules());
}

internal enum RuleKind
{
    Type,
    Union,
    TokenUnion,
}

internal class BoundAlternative
{
    internal required string SourceText { get; init; }
    internal required string EntriesText { get; init; }
    internal int LineCreated { get; init; }
    internal List<BoundAlternativeEntry> Entries { get; } = [];
    internal IEnumerable<BoundAlternativeEntry> Variables
        => Entries.Where(e => e.Quantifier is not QuantifierKind.Lookahead and not QuantifierKind.Cut);
    internal BoundAction? Action { get; set; }

    internal AlternativeIr ToIr(bool isUnion)
    {
        var variables = Variables.Select(v => new VariableIr(v));

        var conditions = Entries.Select(e => e.ToConditionIr());

        var action = Action?.ToIr(variables) ??
            new ActionIr(isUnion ? ActionKind.Passive : ActionKind.Generative, Action?.Type.Name, variables);

        return new AlternativeIr(
            conditions.Any(c => c.Kind == QuantifierKind.Cut),
            SourceText,
            EntriesText,
            variables,
            conditions,
            action);
    }

    internal IEnumerable<BoundRuleAlternativeEntry> GetPotentialLeftRecursive(List<CompilationWarning> warnings)
    {
        int lastIndex = 0;
        for (; lastIndex < Entries.Count; lastIndex++)
        {
            bool stopIterate = false;
            switch (Entries[lastIndex].Quantifier)
            {
                case QuantifierKind.Expect:
                case QuantifierKind.Gather:
                case QuantifierKind.Repeat when Entries[lastIndex].MinRepeatCount == 1:
                    stopIterate = true;
                    break;

                case QuantifierKind.Lookahead:
                case QuantifierKind.Optional:
                case QuantifierKind.Repeat when Entries[lastIndex].MinRepeatCount == 0:
                    continue;

                case QuantifierKind.Cut:
                {
                    warnings.Add(new CompilationWarning
                    {
                        Message = "Cut operator was used but no guarantee that any token was used.",
                        Line = LineCreated,
                    });
                    continue;
                }
            }

            if (stopIterate)
            {
                // To include current index to return list because range operator uses it as stop.
                // If loop will end of reaching end of list, doing it out of loop will cause out of range.
                lastIndex++;
                break;
            }
        }

        return Entries[..lastIndex].OfType<BoundRuleAlternativeEntry>();
    }

    internal bool StartsWith(BoundAlternative other)
    {
        if (other.Entries.WhereNotCut().Count() > Entries.WhereNotCut().Count())
            return false;

        foreach (var (myEntry, otherEntry) in Entries.WhereNotCut().Zip(other.Entries.WhereNotCut()))
        {
            if (myEntry != otherEntry)
                return false;
        }

        return true;
    }

    internal IEnumerable<BoundRule> GetAllUsedRules()
    {
        foreach (var entry in Entries)
        {
            switch (entry)
            {
                case BoundRuleAlternativeEntry r:
                    yield return r.Value;
                    break;

                case BoundGatherAlternativeEntry g:

                    if (g.Value is BoundRuleAlternativeEntry valueRule)
                        yield return valueRule.Value;

                    if (g.Separator is BoundRuleAlternativeEntry separatorRule)
                        yield return separatorRule.Value;

                    break;
            }
        }
    }
}

file static class ListExtensions
{
    extension(List<BoundAlternativeEntry> entries)
    {
        public IEnumerable<BoundAlternativeEntry> WhereNotCut() =>
            entries.Where(e => e.Quantifier != QuantifierKind.Cut);
    }
}

internal class BoundAction
{
    /// <summary>
    /// Type that this action would return if alternative is matched.
    /// </summary>
    internal required BoundRuleType Type { get; init; }

    /// <summary>
    /// Variables that was captured in the <see cref="ActionNode"/>  would be used to generate
    /// type fields.
    /// </summary>
    internal required List<BoundCapturedVariable> CapturedVariables { get; init; }

    internal ActionIr ToIr(IEnumerable<VariableIr> variables) => new(ActionKind.Generative, Type.Name, variables);
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

internal abstract record BoundAlternativeEntry
{
    /// <summary>
    /// Name of the variable that saved to <see cref="GreenNode.Children"/> if match.
    /// Used to identify when in grammar used to create named property.
    /// </summary>
    internal required string Name { get; init; }

    /// <summary>
    /// Index of the entry saved to <see cref="GreenNode.Children"/> to be able retrieve it.
    /// </summary>
    internal int Index { get; init; }

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

    internal ConditionIr ToConditionIr() => new()
    {
        Kind = Quantifier,
        AssignedVar = new VariableIr(this),
        Identifier = Name,
        Positiveness = Positiveness,
        MinCount = MinRepeatCount,
        Atom = getAtom(this)!, // Atom may be null if cut, but cut never uses Atom.
        Separator = getAtom((this as BoundGatherAlternativeEntry)?.Separator),

    };

    internal string? GetTypeName() => this switch
    {
        BoundRuleAlternativeEntry r => r.Value.Type.Name,
        BoundTokenAlternativeEntry or BoundStringAlternativeEntry => "Token",
        _ => null,
    };

    internal bool GetTypeIsUnion() => this is BoundRuleAlternativeEntry r && r.Value.Type is BoundUnionType;

    private static AtomIr? getAtom(BoundAlternativeEntry? alternativeEntry) => alternativeEntry switch
    {
        BoundTokenAlternativeEntry token => new AtomIr(token.Value.ToString(), false, true, false),
        BoundStringAlternativeEntry str => new AtomIr(str.Value, true, false, false),
        BoundRuleAlternativeEntry rule => new AtomIr(rule.Value.Name, false, false, rule.Value.Type is BoundUnionType),
        BoundGatherAlternativeEntry gath => getAtom(gath.Value),
        BoundCutAlternativeEntry => null,
        null => null,
        _ => throw new UnreachableException("Unexpected bound alternative entry class."),
    };
}

internal record BoundCutAlternativeEntry : BoundAlternativeEntry
{
    [SetsRequiredMembers]
    internal BoundCutAlternativeEntry()
    {
        Name = "_cut";
        Quantifier = QuantifierKind.Cut;
    }
}

internal record BoundRuleAlternativeEntry : BoundAlternativeEntry
{
    internal required BoundRule Value { get; init; }
}

internal record BoundTokenAlternativeEntry : BoundAlternativeEntry
{
    internal required TokenType Value { get; init; }
}

internal record BoundStringAlternativeEntry : BoundAlternativeEntry
{
    internal required string Value { get; init; }
}

internal record BoundGatherAlternativeEntry : BoundAlternativeEntry
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
        IsAbstract = false,
        Name = "Token",
    };

    internal TypeIr ToIr() => new(
        this is BoundRuleType ? TypeKind.Node : TypeKind.Union,
        (this as BoundRuleType)?.Fields.Select(f => new FieldIr(f, AccessModifier.Internal)) ?? [],
        AccessModifier.Internal,
        Name,
        (this as BoundRuleType)?.Base?.Name,
        (this as BoundRuleType)?.IsAbstract,
        UnionMembership.Select(u => u.Name)
    );
}

internal sealed class BoundRuleType : BoundType
{
    internal required BoundRuleType? Base { get; init; }
    internal required bool IsAbstract { get; init; }
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
