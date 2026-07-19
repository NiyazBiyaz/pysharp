using System.Diagnostics.CodeAnalysis;

namespace PySharp.SyntaxAnalysis.Generator;

internal record RuleIr(
    string SourceText,
    string Name,
    RuleKind Kind,
    bool IsMemoEnabled,
    bool IsLeftRecursive,
    IEnumerable<AlternativeIr> Alternatives);


internal record VariableIr(string Name, bool IsArray, bool IsOptional, string? TypeName, bool TypeIsUnion)
{
    internal VariableIr(BoundAlternativeEntry entry)
        : this(
            entry.Name,
            entry.Quantifier.IsArray,
            entry.Quantifier == QuantifierKind.Optional,
            entry.GetTypeName(),
            entry.GetTypeIsUnion())
    {
    }
}

internal record ConditionIr
{
    public required QuantifierKind Kind { get; init; }
    public required VariableIr? AssignedVar { get; init; }
    public required string Identifier { get; init; }
    public required bool? Positiveness { get; init; }
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
    string EntriesText,
    IEnumerable<VariableIr> Variables,
    IEnumerable<ConditionIr> Conditions,
    ActionIr Action);

internal record AtomIr(string CallData, bool IsString, bool IsToken, bool IsUnion)
{
    internal string Usage => this switch
    {
        { IsString: true, IsToken: false } => $@"Expect(""{CallData}"")",
        { IsString: false, IsToken: true } => $"Expect(TokenType.{CallData})",
        { IsString: false, IsToken: false } => $"rule_{CallData}()",
        _ => throw new ArgumentException(nameof(IsString) + "&" + nameof(IsToken)),
    };
}

internal enum TypeKind
{
    Node,
    Union,
}

internal record TypeIr(
    TypeKind Kind,
    IEnumerable<FieldIr> Fields,
    AccessModifier AccessModifier,
    string Name,
    string? BaseName,
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
    internal required bool TypeIsUnion { get; init; }

    [SetsRequiredMembers]
    internal FieldIr(BoundField boundField, AccessModifier accessModifier)
    {
        Name = boundField.Name;
        TypeName = boundField.Type.Name;
        Kind = boundField.Kind;
        ChildIndex = boundField.Index;
        AccessModifier = accessModifier;
        IsOptional = boundField.IsOptional;
        TypeIsUnion = boundField.Type is BoundUnionType;
    }
}
