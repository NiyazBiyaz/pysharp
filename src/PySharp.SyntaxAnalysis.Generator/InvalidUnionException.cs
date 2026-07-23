namespace PySharp.SyntaxAnalysis.Generator;

internal class InvalidUnionException(string? reason) : CompilationException($"Union and Token union rules {reason}");
