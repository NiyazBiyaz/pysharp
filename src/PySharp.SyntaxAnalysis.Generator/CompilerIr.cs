using System.Diagnostics;

namespace PySharp.SyntaxAnalysis.Generator;

internal class AtomIr
{
    public required string Name { get; init; }
    public required string Value { get; init; }
    public required bool IsToken { get; init; }
    public required bool IsString { get; init; }
    public RuleIr? LinkedRule { get; set; }
}

internal class SymbolIr
{
    public virtual required SymbolKind Kind { get; init; }
    public required AtomIr Atom { get; init; }

    public string Name
    {
        get => Atom.Name + Kind switch
        {
            SymbolKind.Atom or SymbolKind.Optional => "",
            SymbolKind.Repeat0 => "Star",
            SymbolKind.Repeat1 => "Plus",

            SymbolKind.LookPositive or SymbolKind.LookNegative
                => throw new UnreachableException("Creating name for the lookahead symbol is wrong."),

            SymbolKind.Gather => "Gathered",

            _ => throw new UnreachableException($"Invalid SymbolKind value: {Kind}"),
        };
    }
}

internal class GatherSymbolIr : SymbolIr
{
    public required AtomIr Separator { get; init; }
}

internal enum SymbolKind
{
    Atom,
    Repeat0,
    Repeat1,
    LookPositive,
    LookNegative,
    Optional,
    Gather,
}

internal class AlternativeIr
{
    public required string OriginalText { get; init; }
    public required List<SymbolIr> Symbols { get; init; }
    public ActionIr? Action { get; set; }
}

internal class ActionIr(TypeIr type, List<TargetIr> targets)
{
    public TypeIr ConstructibleType { get; set; } = type;
    public List<TargetIr> Targets { get; set; } = targets;
}

internal class TargetIr(SymbolIr? targetSymbol)
{
    public SymbolIr? Symbol { get; init; } = targetSymbol;
    public string? AxisName { get; init; }
    public bool IsString { get; init; } = false;
    public bool IsParseString { get; init; } = false;
    public bool IsGroupAxis { get; init; } = false;
    public bool IsArrayWrapper { get; init; } = false;
    public bool IsBoolConst { get; init; } = false;
    public bool BoolConstValue { get; init; }
}

[DebuggerDisplay("rule: {Name}")]
internal class RuleIr(string name, TypeIr type, string text)
{
    public string Name { get; set; } = name;
    public TypeIr Type { get; } = type;
    public string OriginalText { get; init; } = text;
    public required List<AlternativeIr> Alternatives { get; set; }
    public bool IsUnion { get; set; } = false;
    public bool IsAnonymous { get; set; } = false;
}

internal class TypeIr(string name)
{
    public string Name { get; init; } = name;
    public RuleIr? Rule { get; set; }
}
