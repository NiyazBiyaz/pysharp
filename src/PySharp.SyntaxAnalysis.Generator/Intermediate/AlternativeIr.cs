using System.Diagnostics.CodeAnalysis;

namespace PySharp.SyntaxAnalysis.Generator.Intermediate;

[method: SetsRequiredMembers]
internal class AlternativeIr(string source, IEnumerable<ISymbolIr> symbols, string? successExpr = null)
{
    public required string SourceText { get; init; } = source;
    public required IEnumerable<ISymbolIr> Symbols { get; init; } = symbols;
    public string? SuccessExpression { get; set; } = successExpr;
}
