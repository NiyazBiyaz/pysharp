using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Common;

public readonly struct SyntaxViewTree
{
    public required IRedView Root { get; init; }
    public required TextPositionMap PositionMap { get; init; }
}
