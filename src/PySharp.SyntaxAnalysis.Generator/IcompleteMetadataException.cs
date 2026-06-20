namespace PySharp.SyntaxAnalysis.Generator;

internal class IncompleteMetadataException : CompilationException
{
    public IncompleteMetadataException(string metadataField)
        : base($"Incomplete metadata. Grammar must have field: {metadataField}.")
    {
    }
}
