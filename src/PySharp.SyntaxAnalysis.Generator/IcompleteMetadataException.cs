namespace PySharp.SyntaxAnalysis.Generator;

public class IncompleteMetadataException : Exception
{
    public IncompleteMetadataException(string metadataField)
        : base($"Incomplete metadata. Grammar must have field: {metadataField}.")
    {
    }
}
