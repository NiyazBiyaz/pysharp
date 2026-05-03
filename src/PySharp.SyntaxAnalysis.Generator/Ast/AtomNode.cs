using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Generator.Ast;

internal abstract record AtomNode : GreenNode
{
    public string Value { get; protected init; } = null!;
}
