namespace PySharp.SyntaxAnalysis.Generator;

internal record CompilationWarning
{
    internal required string Message { get; init; }
    internal required int Line { get; init; }
}
