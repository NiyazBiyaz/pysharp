using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Generator.Ast;

internal record MoleculeNode : GreenNode
{
    public AtomNode Atom { get; protected init; } = null!;
}
