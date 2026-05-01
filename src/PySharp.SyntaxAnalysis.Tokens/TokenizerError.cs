namespace PySharp.SyntaxAnalysis.Tokens;

public enum TokenizerError
{
    NoError,
    InvalidLineContinuation,
    InvalidLiteral,
    IndentationError,
    CharacterError,
    PartialNestingOverflow,
    PartialTooLongExpression,
    PartialUnclosedExpression,
}
