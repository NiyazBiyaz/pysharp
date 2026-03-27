namespace PySharp.Tokens;

public enum TokenizerError
{
    NoError,
    InvalidLineContinuation,
    InvalidLiteral,
    IndentationError,
    CharacterError,
}
