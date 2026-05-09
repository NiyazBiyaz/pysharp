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
    public required SymbolKind Kind { get; init; }
    public required AtomIr Atom { get; set; }

    public string Name
    {
        get => Atom.Name + Kind switch
        {
            SymbolKind.Atom or SymbolKind.Optional => "",
            SymbolKind.Repeat0 => "Star",
            SymbolKind.Repeat1 => "Plus",

            SymbolKind.LookPositive or SymbolKind.LookNegative
                => throw new UnreachableException("Creating name for the lookahead symbol is wrong."),

            _ => throw new UnreachableException($"Invalid SymbolKind value: {Kind}"),
        };
    }
}

internal enum SymbolKind
{
    Atom,
    Repeat0,
    Repeat1,
    LookPositive,
    LookNegative,
    Optional,
}

internal class AlternativeIr
{
    public required string OriginalText { get; init; }
    public required List<SymbolIr> Symbols { get; init; }
    public required string ReturnExpression { get; init; }
}

[DebuggerDisplay("rule: {Name}")]
internal class RuleIr(string name, TypeIr type, string text)
{
    public string Name { get; set; } = name;
    public TypeIr Type { get; } = type;
    public string OriginalText { get; init; } = text;
    public List<AlternativeIr> Alternatives { get; set; } = null!;
    public bool IsUnion { get; set; } = false;
}

internal class TypeIr(string name)
{
    public string Name { get; init; } = name;
}
