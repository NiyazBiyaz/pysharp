namespace PySharp.Tokenizer;

public enum TokenizerError
{
    NoError,
    Completed,
    InvalidLineContinuation,
    InvalidLiteral,
    IndentationError,
    CharacterError,
}
