namespace PySharp.SyntaxAnalysis.Generator.Intermediate;

internal record QuantifiedSymbolIr : ISymbolIr
{
    public ISymbolIr Inner { get; private init; } = null!;
    public bool IsVirtual { get; private init; }
    public QuantifierKind Kind { get; private init; }
    public string? Name { get; private init; }
    public string? TypeName { get; private init; }
    public int? RepeatCount { get; private init; }
    public bool? Positiveness { get; private init; }

    public static QuantifiedSymbolIr CreateRepeat(ISymbolIr inner, int minCount)
    {
        if (minCount < 0 || minCount > 1)
            throw new ArgumentOutOfRangeException(nameof(minCount));

        var suffix = minCount == 0 ? "Star" : "Plus";

        var name = inner.Name + suffix;
        var type = $"NodeArray<{inner.TypeName}>";

        return new QuantifiedSymbolIr()
        {
            Inner = inner,
            Kind = QuantifierKind.Repeat,
            Name = name,
            TypeName = type,
            IsVirtual = false,
            RepeatCount = minCount,
            Positiveness = null,
        };
    }

    public static QuantifiedSymbolIr CreateOptional(ISymbolIr inner)
    {
        return new()
        {
            Inner = inner,
            Kind = QuantifierKind.Optional,
            Name = inner.Name,
            TypeName = inner.TypeName,
            IsVirtual = false,
            RepeatCount = null,
            Positiveness = null,
        };
    }

    public static QuantifiedSymbolIr CreateLookahead(ISymbolIr inner, bool positiveness)
    {
        return new()
        {
            Inner = inner,
            Kind = QuantifierKind.Lookahead,
            Name = null,
            TypeName = null,
            IsVirtual = true,
            RepeatCount = null,
            Positiveness = positiveness,
        };
    }
}
