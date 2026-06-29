namespace PySharp.SyntaxAnalysis.Generator;

internal record RuleIr(string OriginalText, string Name, string ReturnTypeName);

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
    public required AtomIr Atom { get; init; }
    public required AtomIr? Separator { get; init; }
}

internal record AtomIr(string CallData, bool IsString, bool IsToken);
