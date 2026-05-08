using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Generator.Intermediate;

internal record TokenSymbolIr(string Name, string ExpectInterpolation) : ISymbolIr
{
    public bool IsVirtual => false;
    public string TypeName => nameof(TokenNode);
}
