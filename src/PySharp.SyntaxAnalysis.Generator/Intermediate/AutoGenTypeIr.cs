namespace PySharp.SyntaxAnalysis.Generator.Intermediate;

internal record AutoGenTypeIr(string Name, IEnumerable<(string typeName, string varName)> Properties, string BaseClass);
