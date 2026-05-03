namespace PySharp.SyntaxAnalysis.Generator;

internal class IncompleteMetadataException : Exception
{
    public IncompleteMetadataException(string metadataField)
        : base($"Incomplete metadata. Grammar must have field: {metadataField}.")
    {
    }
}
