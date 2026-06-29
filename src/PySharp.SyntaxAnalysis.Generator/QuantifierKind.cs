namespace PySharp.SyntaxAnalysis.Generator;

internal enum QuantifierKind
{
    Expect,
    Lookahead,
    Repeat,
    Optional,
    Gather,
    Cut,
}

internal static class QuantifierKindExtensions
{
    extension(QuantifierKind quantifier)
    {
        internal bool IsArray => quantifier == QuantifierKind.Repeat || quantifier == QuantifierKind.Gather;

        internal string GetSuffix(int? repCount) => quantifier switch
        {
            QuantifierKind.Expect or QuantifierKind.Lookahead or QuantifierKind.Optional => "",
            QuantifierKind.Repeat => repCount == 1 ? "Plus" : "Star",
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}
