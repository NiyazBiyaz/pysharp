using System.Diagnostics;

namespace PySharp.SyntaxAnalysis.Generator.Intermediate;

internal record RuleSymbolIr(IRuleIr Rule) : ISymbolIr
{
    public bool IsVirtual => false;
    public string TypeName => Rule.ReturnType ?? throw new UnreachableException("Unresolved rule type.");
    public string Name => Rule.Name.ToLowerInvariant() ?? throw new UnreachableException("Unresolved rule name.");
}
