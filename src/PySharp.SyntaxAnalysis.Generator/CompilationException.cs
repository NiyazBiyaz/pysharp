namespace PySharp.SyntaxAnalysis.Generator;

internal class CompilationException(string? message) : Exception(message)
{
    internal required int Line { get; init; }
}
