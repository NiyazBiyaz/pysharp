namespace PySharp.SyntaxAnalysis.Generator;

internal class CompilationException(string? message) : Exception(message)
{
    internal int Line => -1; // TODO: add errors positioning.
}
