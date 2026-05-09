namespace PySharp.SyntaxAnalysis.Generator.Ast;

internal abstract record RepeatMoleculeNode : MoleculeNode
{
    public abstract int MinCount { get; }
}
