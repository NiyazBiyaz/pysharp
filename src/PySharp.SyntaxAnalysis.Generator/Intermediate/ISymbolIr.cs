namespace PySharp.SyntaxAnalysis.Generator.Intermediate;

internal interface ISymbolIr
{
    bool IsVirtual { get; }
    string? TypeName { get; }
    string? Name { get; }
}
