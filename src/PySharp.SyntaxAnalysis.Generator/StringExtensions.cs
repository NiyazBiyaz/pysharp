namespace PySharp.SyntaxAnalysis.Generator;

internal static class StringExtensions
{
    public static string WrapNullCheck(this string str, string name) => $"({name} = {str}) is not null";
}
