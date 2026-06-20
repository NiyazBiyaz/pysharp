using System.Collections.ObjectModel;

namespace PySharp.SyntaxAnalysis.Generator;

internal record GrammarData(
    ReadOnlyDictionary<string, string> MetadataFields,
    List<RuleData> Rules,
    List<TypeData> Types,
    List<string> Keywords
);

internal record RuleData(
    string Name,
    string ReturnName,
    List<AlternativeData> Alternatives,
    string OriginalText,
    bool IsUnion,
    bool IsAnonymous
// TODO: bool IsLeftRecursive
// TODO: bool EnableMemo
);

internal record TypeData(string Name, TypeAccessModifier AccessModifier, List<VariableData> Fields);

internal enum TypeAccessModifier
{
    Anonymous,
    Public,
}

internal record AlternativeData(
    string OriginalText,
    List<VariableData> Variables,
    List<ConditionData> Conditions,
    bool HasOptionals,
    string ReturnTypeName,
    List<CtorArgumentData> CtorArguments
);

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

internal record VariableData(string Name, string TypeName, bool NeedWrapper, bool IsOptional);

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
