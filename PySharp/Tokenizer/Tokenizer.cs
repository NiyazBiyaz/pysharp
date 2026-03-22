using System.Diagnostics;

namespace PySharp.Tokenizer;

[DebuggerDisplay("next={nextChar.ToString()} ln={lineNumber} cl={currentColumnOffset} len={currentPos - startPos} lex={source[startPos..currentPos]}")]
public class Tokenizer(string source, bool saveTrivia)
{
    private readonly string source = source;
    private readonly bool saveTrivia = saveTrivia;

    private int currentPos = 0;
    private int startPos = 0;
    private bool atLineBeginning = true;
    private int lineNumber = 0;
    private int startLineNumber = -1;

    private int startColumnOffset = 0;
    private int currentColumnOffset = 0;

    private bool isBlankLine = false;
    private bool isBlankLineWithComment;

    private readonly Stack<int> indentStack = new([0]);
    private readonly Stack<int> alternateIndentStack = new([0]);

    private const char eof = '\0';
    private const int tab_size = 8;
    private const int alter_tab_size = 1;
    private const string invalid_dec = "Invalid decimal literal.";

    private int pendingIndentation
    {
        get => field;
        set
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 1);
            field = value;
        }
    } = 0;
    private int bracketsLevel
    {
        get => field;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            field = value;
        }
    } = 0;

    /// <summary>
    /// Non-inclusive end of the current token's lexeme.
    /// </summary>
    private char nextChar => lookAt(currentPos, 0);
    private char nextNextChar => lookAt(currentPos, 1);

    public bool ShouldStop { get; private set; } = false;

    public TokenizerError Error = TokenizerError.NoError;
    public string? ErrorMessage = null;

    public Token ReadNext()
    {
        resetStart();

        Token? maybeToken;
        maybeToken = tryNextLine() ?? tryIndentation();
        if (maybeToken.HasValue)
            return maybeToken.Value;

        skipWhitespace();
        resetStart();

        maybeToken =
            tryComment() ??
            tryEof() ??
            tryName() ??
            tryLineFeed() ??
            tryDotOrFraction() ??
            tryNumber() ??
            tryString() ??
            tryLineContinuation();

        if (maybeToken.HasValue)
            return maybeToken.Value;

        throw new NotImplementedException();
    }

    private Token? tryNextLine()
    {
        if (atLineBeginning)
        {
            atLineBeginning = false;

            int column = 0;
            int alternateColumn = 0;
            int continuationColumn = 0;

            while (true)
            {
                if (nextChar == ' ' || nextChar == '\f')
                {
                    column++;
                    alternateColumn++;
                    moveNext();
                }
                else if (nextChar == '\t')
                {
                    column = (column / tab_size + 1) * tab_size;
                    alternateColumn = (alternateColumn / alter_tab_size + 1) * alter_tab_size;
                    moveNext();
                }
                else if (nextChar == '\\')
                {
                    continuationColumn = continuationColumn != 0 ? continuationColumn : column;
                    var lineCont = readContinuationLine();
                    if (lineCont.Type is TokenType.Error)
                        return lineCont;
                }
                else
                    break;
            }

            isBlankLine = nextChar == '#' || nextChar == '\n';

            if (!isBlankLine && bracketsLevel == 0)
            {
                column = continuationColumn == 0 ? column : continuationColumn;
                alternateColumn = continuationColumn == 0 ? alternateColumn : continuationColumn;

                string tabSpaceErrMsg = "Tabs and spaces mixing is not allowed.";

                if (column == indentStack.Peek())
                {
                    if (alternateColumn != alternateIndentStack.Peek())
                        return errorToken(TokenizerError.IndentationError, tabSpaceErrMsg);
                }
                else if (column > indentStack.Peek())
                {
                    if (alternateColumn <= alternateIndentStack.Peek())
                        return errorToken(TokenizerError.IndentationError, tabSpaceErrMsg);

                    pendingIndentation++;
                    indentStack.Push(column);
                    alternateIndentStack.Push(alternateColumn);
                }
                else if (column < indentStack.Peek())
                {
                    // column cannot be lower than zero here, so it will stop on indentStack[0] == 0
                    while (column < indentStack.Peek())
                    {
                        pendingIndentation--;
                        indentStack.Pop();
                        alternateIndentStack.Pop();
                    }

                    if (column != indentStack.Peek())
                        return errorToken(TokenizerError.IndentationError, "Can dedent only on existing indentation level.");

                    if (alternateColumn != alternateIndentStack.Peek())
                        return errorToken(TokenizerError.IndentationError, tabSpaceErrMsg);
                }
            }
        }

        return null;
    }

    private Token? tryIndentation()
    {
        // Leave lexemes of indentation tokens empty.
        if (pendingIndentation < 0)
        {
            pendingIndentation++;
            return createToken(TokenType.Dedent, true);
        }
        else if (pendingIndentation > 0)
        {
            pendingIndentation--;
            return createToken(TokenType.Indent, true);
        }

        return null;
    }

    private void skipWhitespace()
    {
        while (nextChar == ' ' || nextChar == '\t' || nextChar == '\f')
            moveNext();

        if (saveTrivia)
        {
            throw new NotImplementedException();
        }
    }

    private void resetStart()
    {
        startPos = currentPos;
        startColumnOffset = currentColumnOffset;
    }


    private Token? tryComment()
    {
        if (nextChar == '#')
        {
            // Skip while not on the line feed.
            while (nextChar != '\n')
                moveNext();

            if (saveTrivia)
            {
                isBlankLineWithComment = isBlankLine;
                return createToken(TokenType.Comment);
            }
        }

        return null;
    }

    private Token? tryEof()
    {
        if (nextChar == eof)
            return createToken(TokenType.EndOfFile, true);

        return null;
    }

    private Token? tryName()
    {
        if (isPotentialNameStart(nextChar))
        {
            int iters = 0;
            bool sawB = false, sawR = false, sawU = false, sawF = false, sawT = false;
            // Try to read prefixed string.
            while (iters < 2)
            {
                if (!sawB && (nextChar == 'b' || nextChar == 'B'))
                    sawB = true;

                else if (!sawR && (nextChar == 'r' || nextChar == 'R'))
                    sawR = true;

                else if (!sawU && (nextChar == 'u' || nextChar == 'U'))
                    sawU = true;

                else if (!sawF && (nextChar == 'f' || nextChar == 'F'))
                    sawF = true;

                else if (!sawF && (nextChar == 't' || nextChar == 'T'))
                    sawT = true;

                else
                    break;

                moveNext();
                iters++;

                if (nextChar == '\'' || nextChar == '"')
                {
                    if (invalidStringPrefixes(sawB, sawR, sawU, sawF, sawT) is string errMsg)
                        return errorToken(TokenizerError.InvalidLiteral, errMsg);

                    if (sawF || sawT)
                        return readPartialStringStart(sawF ? PartialStringType.FormatString : PartialStringType.TemplateString, sawR);

                    return readString();
                }
            }

            while (isPotentialNameChar(nextChar))
                moveNext();

            return createToken(TokenType.Name);
        }

        return null;
    }

    private Token? tryLineFeed()
    {
        if (nextChar == '\n')
        {
            atLineBeginning = true;
            moveNext();

            if (isBlankLine || bracketsLevel > 0)
            {
                if (saveTrivia)
                {
                    if (isBlankLineWithComment)
                        isBlankLineWithComment = false;
                    return createToken(TokenType.TriviaNewLine);
                }
                return tryNextLine();
            }
            if (isBlankLineWithComment && saveTrivia)
            {
                isBlankLineWithComment = false;
                return createToken(TokenType.TriviaNewLine);
            }

            var tok = createToken(TokenType.NewLine);
            lineNumber++;
            currentColumnOffset = 0;
            return tok;
        }

        return null;
    }

    private Token? tryDotOrFraction()
    {
        if (nextChar == '.')
        {
            if (char.IsAsciiDigit(nextNextChar))
                return readDecimalNumber();

            moveNext();
            if (nextChar == '.' && nextNextChar == '.')
            {
                moveNext();
                moveNext();
                return createToken(TokenType.Ellipsis);
            }

            return createToken(TokenType.Dot);
        }

        return null;
    }

    private Token? tryNumber()
    {
        if (char.IsAsciiDigit(nextChar))
        {
            if (nextChar == '0')
            {
                moveNext();
                // Hexadecimal.
                if (char.ToLower(nextChar) == 'x')
                {
                    throw new NotImplementedException("Hexadecimal is not supported yet.");
                }
                // Octal.
                else if (char.ToLower(nextChar) == 'o')
                {
                    throw new NotImplementedException("Octal is not supported yet.");
                }
                // Binary.
                else if (char.ToLower(nextChar) == 'b')
                {
                    throw new NotImplementedException("Binary is not supported yet.");
                }

                // Decimal literal with leading zeros.
                else
                {
                    bool nonZeros = false;

                    while (nextChar == '0')
                    {
                        moveNext();

                        if (nextChar == '_')
                        {
                            moveNext();
                            if (!char.IsAsciiDigit(nextChar))
                                return errorToken(TokenizerError.InvalidLiteral, invalid_dec);
                        }
                    }

                    if (char.IsAsciiDigit(nextChar))
                    {
                        nonZeros = true;
                        bool ok = moveWhileDecimal();
                        if (!ok)
                            return errorToken(TokenizerError.InvalidLiteral, invalid_dec);
                    }
                    if (nextChar == '.')
                        readDecimalNumber();

                    else if (nonZeros && !saveTrivia)
                        return errorToken(TokenizerError.InvalidLiteral, """
                        Leading zeros in decimal integer are not permitted; use an '0o' prefix for octal numbers.
                        """);

                    if (isInvalidEndOfNumber(nextChar))
                        return errorToken(TokenizerError.InvalidLiteral, invalid_dec);
                }

                return createToken(TokenType.Number);
            }

            else
                return readDecimalNumber();
        }

        return null;
    }

    private Token readDecimalNumber()
    {
        // Eat integer part.
        {
            bool ok = moveWhileDecimal();
            if (!ok)
                return errorToken(TokenizerError.InvalidLiteral, invalid_dec);
        }

        // Eat fraction part.
        if (nextChar == '.')
        {
            bool ok = moveFractionPart();
            if (!ok)
                return errorToken(TokenizerError.InvalidLiteral, invalid_dec);
        }

        // Eat exponent part.
        if (char.ToLower(nextChar) == 'e')
        {
            bool ok = moveExponentPart();
            if (!ok)
                return errorToken(TokenizerError.InvalidLiteral, invalid_dec);
        }

        // Eat imaginary part (just one symbol).
        if (char.ToLower(nextChar) == 'j')
            moveNext();

        if (isInvalidEndOfNumber(nextChar))
            return errorToken(TokenizerError.InvalidLiteral, invalid_dec);

        return createToken(TokenType.Number);
    }

    private bool moveFractionPart()
    {
        // Expecting a dot.
        if (nextChar == '.')
            moveNext();
        else
            return false;

        bool ok = moveWhileDecimal();
        return ok;
    }
    private bool moveExponentPart()
    {
        // Expecting a 'e' or 'E'
        if (char.ToLower(nextChar) == 'e')
            moveNext();
        else
            return false;

        // Eat plus or minus in exponent.
        if (nextChar == '+' || nextChar == '-')
        {
            moveNext();
            if (!char.IsAsciiDigit(nextChar))
                return false;
        }
        // Invalid if after exponent not plus, minus or number.
        else if (!char.IsAsciiDigit(nextChar))
            return false;

        bool ok = moveWhileDecimal();
        return ok;
    }

    private Token? tryString() => null;

    private Token? readPartialStringStart(PartialStringType stringType, bool prefixR) => throw new NotImplementedException();

    private Token? readString() => throw new NotImplementedException();

    private Token? tryLineContinuation()
    {
        if (nextChar == '\\')
        {
            return readContinuationLine();
        }

        return null;
    }

    private Token readContinuationLine()
    {
        if (nextChar != '\n')
        {
            if (nextChar == eof)
                return errorToken(TokenizerError.InvalidLineContinuation, "Expected new line.");
            else
                return errorToken(TokenizerError.InvalidLineContinuation, "Any characters is not allowed after explicit line continuation.");
        }

        return ReadNext();
    }

    #region Helpers

    private bool isEof(int position, int offset) => position + offset >= source.Length;

    private void moveNext()
    {
        if (isEof(currentPos, 0))
        {
            ShouldStop = true;
            return;
        }

        currentPos++;
        currentColumnOffset++;

        if (lookAtRaw(currentPos, 0) == '\r')
        {
            if (lookAtRaw(currentPos, 1) == '\n')
            {
                currentPos++;
                currentColumnOffset++;
            }
        }
    }

    private char lookAt(int position, int offset)
    {
        char ch = lookAtRaw(position, offset);

        if (ch == '\n' && lookAtRaw(position, offset - 1) == '\r')
            return lookAtRaw(position, offset - 2);

        return ch == '\r' ? '\n' : ch;
    }

    private char lookAtRaw(int position, int offset) => !isEof(position, offset) ? source[position + offset] : eof;

    private Token createToken(TokenType type, bool emptyLexeme = false)
    {
        int startLine = type switch
        {
            TokenType.String or TokenType.FStringMiddle or TokenType.TStringMiddle => startLineNumber,
            _ => lineNumber,
        };

        ReadOnlyMemory<char> lexeme = emptyLexeme ? ReadOnlyMemory<char>.Empty
                                                  : source.AsMemory(startPos, currentPos - startPos);

        int startColumnOffset = this.startColumnOffset;
        int endColumnOffset = currentColumnOffset;

        var begPos = new TokenPosition() { Line = startLine, Column = startColumnOffset };
        var endPos = new TokenPosition() { Line = lineNumber, Column = endColumnOffset };

        return new()
        {
            Type = type,
            Lexeme = lexeme,
            Start = begPos,
            End = endPos,
        };
    }

    private Token errorToken(TokenizerError error, string message)
    {
        ShouldStop = true;
        Error = error;
        ErrorMessage = message;
        return createToken(TokenType.Error, true);
    }

    private bool moveWhileDecimal()
    {
        while (true)
        {
            while (char.IsAsciiDigit(nextChar))
                moveNext();

            if (nextChar != '_')
                return true;

            moveNext();
            if (!char.IsAsciiDigit(nextChar))
                return false;
        }
    }

    private static bool isInvalidEndOfNumber(char ch)
    {
        if (char.IsAscii(ch) && isPotentialNameChar(ch))
            return true;

        return false;
    }

    private static string? invalidStringPrefixes(bool b, bool r, bool u, bool f, bool t)
    {
        const string err_format = "'{0}' and '{1}' prefixes are incompatible.";
        if (u)
        {
            if (b)
                return string.Format(err_format, 'b', 'u');
            if (r)
                return string.Format(err_format, 'r', 'u');
            if (f)
                return string.Format(err_format, 'f', 'u');
            if (t)
                return string.Format(err_format, 't', 'u');
        }
        if (b)
        {
            if (f)
                return string.Format(err_format, 'f', 'b');
            if (t)
                return string.Format(err_format, 't', 'b');
        }
        if (f && t)
            return string.Format(err_format, 'f', 't');

        return null;
    }

    private static bool isPotentialNameStart(char character) =>
        character == '_' || char.IsLetter(character);

    private static bool isPotentialNameChar(char character) =>
        character == '_' || char.IsAsciiDigit(character) || char.IsLetter(character);

    #endregion
}
