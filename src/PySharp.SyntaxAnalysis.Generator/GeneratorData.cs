using System.Collections.ObjectModel;

namespace PySharp.SyntaxAnalysis.Generator;

internal record GrammarData
{
    public required ReadOnlyDictionary<string, string> MetadataFields { get; init; }
    public required List<RuleData> Rules { get; init; }
    public required List<TypeData> Types { get; init; }
    public required List<string> Keywords { get; init; }
}

internal record RuleData
{
    public required string Name { get; init; }
    public required string ReturnName { get; init; }
    public required string OriginalText { get; init; }
    public required List<AlternativeData> Alternatives { get; init; }
    public required bool IsUnion { get; init; }
    public required bool IsAnonymous { get; init; }
    // TODO: public required bool IsLeftRecursive { get; init; }
}

internal record TypeData
{
    public required string Name { get; init; }
    public required TypeAccessModifier AccessModifier { get; init; }
    public required List<VariableData> Fields { get; init; }
}

internal enum TypeAccessModifier
{
    Anonymous,
    Public,
}

internal record AlternativeData
{
    public required string OriginalText { get; init; }
    public required List<VariableData> Variables { get; init; }
    public required List<ConditionData> Conditions { get; init; }
    public required bool HasOptionals { get; init; }
    public required string ReturnTypeName { get; init; }
    public required List<CtorArgumentData> CtorArguments { get; init; }
}

internal record CtorArgumentData(
    CtorArgumentType CtorArgumentType,
    string? VariableName,
    string? AxisName = null,
    bool? BoolConstant = null
);

internal enum CtorArgumentType
{
    Raw,
    String,
    ParseString,
    WrapArray,
    GroupAxis,
    GroupAxisString,
    GroupAxisParseString,
    BoolConstant
}

internal record VariableData
{
    public required string Name { get; init; }
    public required string TypeName { get; init; }
    public required bool NeedWrapper { get; init; }
    public required bool IsOptional { get; init; }
}

internal record ConditionData
{
    public required ConditionKind Kind { get; init; }
    public string? AssignedVar { get; init; }
    public bool? Positive { get; init; }
    public int? MinCount { get; init; }
    public required AtomData Atom { get; init; }
    public AtomData? Separator { get; init; }
}

internal enum ConditionKind
{
    Expect,
    Rule,
    Lookahead,
    Repeat,
    Optional,
    Gather,
}

internal record AtomData
{
    internal string CallData { get; init; }
    internal bool IsString { get; init; }
    internal bool IsToken { get; init; }

    internal AtomData(AtomIr atomIr)
    {
        CallData = atomIr.Value;
        IsString = atomIr.IsString;
        IsToken = atomIr.IsToken;
    }
}
