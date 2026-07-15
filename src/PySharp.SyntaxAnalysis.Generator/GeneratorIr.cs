using System.Diagnostics.CodeAnalysis;

namespace PySharp.SyntaxAnalysis.Generator;

internal record RuleIr(
    string OriginalText,
    string Name,
    string ReturnTypeName,
    bool IsMemoEnabled,
    bool IsLeftRecursive,
    IEnumerable<AlternativeIr> Alternatives);


internal record VariableIr(string Name, bool IsArray, bool IsOptional)
{
    internal string? TypeName { get; init; }
}

internal record ConditionIr
{
    public required QuantifierKind Kind { get; init; }
    public required string? AssignedVar { get; init; }
    public required bool? Positive { get; init; }
    public required int? MinCount { get; init; }
    public required AtomIr Atom { get; init; } = null!;
    public required AtomIr? Separator { get; init; }
}

internal enum ActionKind
{
    Generative,
    Passive,
}

internal record ActionIr(ActionKind Kind, string? TypeName, IEnumerable<VariableIr> Variables);

internal record AlternativeIr(
    bool HasCut,
    string SourceText,
    IEnumerable<VariableIr> Variables,
    IEnumerable<ConditionIr> Conditions,
    ActionIr Action);

internal record AtomIr(string CallData, bool IsString, bool IsToken);

internal enum TypeKind
{
    Rule,
    Union,
}

internal record TypeIr(
    TypeKind Kind,
    IEnumerable<FieldIr> Fields,
    AccessModifier AccessModifier,
    string Name,
    string BaseName,
    bool? IsAbstract,
    IEnumerable<string> UnionMembership);

internal record FieldIr
{
    internal required AccessModifier AccessModifier { get; init; }
    internal required string Name { get; init; }
    internal required string TypeName { get; init; }
    internal required FieldKind Kind { get; init; }
    internal required int ChildIndex { get; init; }
    internal required bool IsOptional { get; init; }

    [SetsRequiredMembers]
    internal FieldIr(BoundField boundField, AccessModifier accessModifier)
    {
        Name = boundField.Name;
        TypeName = boundField.Type.Name;
        Kind = boundField.Kind;
        ChildIndex = boundField.Index;
        AccessModifier = accessModifier;
        IsOptional = boundField.IsOptional;
    }
}
