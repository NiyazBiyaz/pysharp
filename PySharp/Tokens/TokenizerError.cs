namespace PySharp.Tokens;

public enum TokenizerError
{
    NoError,
    Completed,
    InvalidLineContinuation,
    InvalidLiteral,
    IndentationError,
    CharacterError,
}
