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
        internal bool IsArray => quantifier is QuantifierKind.Repeat or QuantifierKind.Gather;

        internal string GetSuffix(int? repCount) => quantifier switch
        {
            QuantifierKind.Expect or QuantifierKind.Optional => "",
            QuantifierKind.Repeat => repCount == 1 ? "Plus" : "Star",
            QuantifierKind.Lookahead => "_",
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}
