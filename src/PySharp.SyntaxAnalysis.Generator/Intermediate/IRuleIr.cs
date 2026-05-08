namespace PySharp.SyntaxAnalysis.Generator.Intermediate;

internal interface IRuleIr
{
    string Name { get; }
    string ReturnType { get; }
    IEnumerable<AlternativeIr> Alternatives { get; set; }
}
