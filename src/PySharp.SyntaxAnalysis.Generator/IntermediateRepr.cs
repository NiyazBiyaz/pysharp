using System.Diagnostics;
using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Generator;

record GrammarIr(IEnumerable<IRuleIr> Rules, string Header, string ParseCallReturnType, string ClassSignature);

interface IRuleIr
{
    string Name { get; }
    string ReturnType { get; }
    IEnumerable<AlternativeIr> Alternatives { get; set; }
}

class NamedTypedRuleIr(string name, string returnType) : IRuleIr
{
    public string Name => name;

    public string ReturnType => returnType;

    public IEnumerable<AlternativeIr> Alternatives { get; set; } = null!;
}


record AlternativeIr(string SourceText, IEnumerable<ISymbolIr> Symbols, string SuccessExpression);

interface ISymbolIr
{
    bool IsVirtual { get; }
    string? TypeName { get; }
    string? Name { get; }
}

record TokenSymbolIr(string Name, string ExpectInterpolation) : ISymbolIr
{
    public bool IsVirtual => false;
    public string TypeName => nameof(TokenNode);
}

record RuleSymbolIr(IRuleIr Rule) : ISymbolIr
{
    public bool IsVirtual => false;
    public string TypeName => Rule.ReturnType ?? throw new UnreachableException("Unresolved rule type.");
    public string Name => Rule.Name.ToLowerInvariant() ?? throw new UnreachableException("Unresolved rule name.");
}

record QuantifiedSymbolIr : ISymbolIr
{
    public ISymbolIr Inner { get; private init; } = null!;
    public bool IsVirtual { get; private init; }
    public Quantifier Kind { get; private init; }
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
            Kind = Quantifier.Repeat,
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
            Kind = Quantifier.Optional,
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
            Kind = Quantifier.Lookahead,
            Name = null,
            TypeName = null,
            IsVirtual = true,
            RepeatCount = null,
            Positiveness = positiveness,
        };
    }
}

enum Quantifier
{
    Repeat,
    Lookahead,
    Optional,
}
