namespace PySharp.SyntaxAnalysis.Generator.Intermediate;

internal class NamedTypedRuleIr(string name, string returnType) : IRuleIr
{
    public string Name => name;

    public string ReturnType => returnType;

    public IEnumerable<AlternativeIr> Alternatives { get; set; } = null!;
}
