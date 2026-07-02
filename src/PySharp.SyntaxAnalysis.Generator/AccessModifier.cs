namespace PySharp.SyntaxAnalysis.Generator;

internal enum AccessModifier
{
    Internal,
    Public,
}

internal static class AccessModifierExtensions
{
    extension(AccessModifier accessModifier)
    {
        internal string CodeRepresentation() => accessModifier switch
        {
            AccessModifier.Internal => "internal",
            AccessModifier.Public => "public",
            _ => throw new ArgumentOutOfRangeException(),
        };
    }
}
