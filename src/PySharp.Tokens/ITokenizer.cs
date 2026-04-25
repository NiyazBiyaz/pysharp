namespace PySharp.Tokens;

public interface ITokenizer
{
    bool ShouldStop { get; }
    TokenizerError Error { get; }
    string? ErrorMessage { get; }

    SynchronizationPoint Synchronize();
    Token ReadNext();
}
