namespace PySharp.SyntaxAnalysis.Generator;

internal class UndeclaredUsageUserException(string? subject, string? name)
    : CompilationException($"In {subject} has no '{name}' found.")
{
}
