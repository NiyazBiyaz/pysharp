using System.Diagnostics;

namespace PySharp.Tokens;

[DebuggerDisplay("next={nextChar.ToString()} ln={lineNumber} cl={currentColumnOffset} lex={source[startPos..currentPos]} len={currentPos - startPos}")]
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
    private bool isBlankLineWithComment = false;
    private bool atContinuedLine = false;

    private readonly Stack<int> indentStack = new([0]);
    private readonly Stack<int> alternateIndentStack = new([0]);

    private const char eof = '\0';
    private const int tab_size = 8;
    private const int alter_tab_size = 1;
    private const string invalid_dec = "Invalid decimal literal.";
    private const string invalid_imaginary = "Invalid imaginary literal.";
    private const string invalid_hex = "Invalid hexadecimal literal.";
    private const string invalid_oct = "Invalid octal literal.";
    private const string invalid_bin = "Invalid binary literal.";
    private const string tab_space_err_msg = "Tabs and spaces mixing is not allowed.";

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
        Token? maybeToken = tryNextLine() ?? tryIndentation();
        if (maybeToken is Token token)
            return token;

        resetStart();
        maybeToken = skipAndTryWhitespace();
        if (maybeToken is Token whiteSpace)
            return whiteSpace;

        resetStart();
        maybeToken = tryComment();
        if (maybeToken is Token comment)
            return comment;

        resetStart();
        Token definitelyToken =
            tryEof() ??
            tryName() ??
            tryLineFeed() ??
            tryDotOrFraction() ??
            tryNumber() ??
            tryString() ??
            tryLineContinuation() ??
            readOperatorOrErrorToken();

        return definitelyToken;
    }

    private Token? tryNextLine()
    {
        if (atLineBeginning)
        {
            atLineBeginning = false;

            // Remain whitespace if we in brackets or on continued line for trivia whitespace tokens.
            if (bracketsLevel != 0 || atContinuedLine)
            {
                atContinuedLine = false;
                return null;
            }

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
                    var lineCont = readLineContinuation();
                    if (lineCont.Type is TokenType.Error)
                        return lineCont;
                }
                else
                    break;
            }

            isBlankLine = nextChar == '#' || nextChar == '\n';

            if (!isBlankLine)
            {
                column = continuationColumn == 0 ? column : continuationColumn;
                alternateColumn = continuationColumn == 0 ? alternateColumn : continuationColumn;

                if (column == indentStack.Peek())
                {
                    if (alternateColumn != alternateIndentStack.Peek())
                        return emptyErrorToken(TokenizerError.IndentationError, tab_space_err_msg);
                }
                else if (column > indentStack.Peek())
                {
                    if (alternateColumn <= alternateIndentStack.Peek())
                        return emptyErrorToken(TokenizerError.IndentationError, tab_space_err_msg);

                    pendingIndentation++;
                    indentStack.Push(column);
                    alternateIndentStack.Push(alternateColumn);
                }
                else if (column < indentStack.Peek())
                {
                    if (enqueueDedent(column, alternateColumn) is Token errTok)
                        return errTok;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Reduces current indentation stack top element to <paramref name="column"/> and enqueues
    /// <see cref="TokenType.Dedent"/> tokens to <see cref="pendingIndentation"/>.
    /// </summary>
    /// <param name="column">Target indentation.</param>
    /// <param name="alternateColumn">Target alternate indentation, needed for indent consistency checks.</param>
    /// <returns><see cref="null"/> on success; otherwise <see cref="TokenType.Error"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"/>
    private Token? enqueueDedent(int column, int alternateColumn)
    {
        Debug.Assert(column < indentStack.Peek());

        ArgumentOutOfRangeException.ThrowIfNegative(column);
        ArgumentOutOfRangeException.ThrowIfNegative(alternateColumn);

        // column cannot be lower than zero here, so it will stop on indentStack[0] == 0
        while (column < indentStack.Peek())
        {
            pendingIndentation--;
            indentStack.Pop();
            alternateIndentStack.Pop();
        }

        if (column != indentStack.Peek())
            return emptyErrorToken(TokenizerError.IndentationError, "Can dedent only on existing indentation level.");

        if (alternateColumn != alternateIndentStack.Peek())
            return emptyErrorToken(TokenizerError.IndentationError, tab_space_err_msg);

        return null;
    }

    private Token? tryIndentation()
    {
        if (pendingIndentation < 0)
        {
            pendingIndentation++;
            // Dedent tokens are empty with zero-width.
            resetStart();
            return createEmptyToken(TokenType.Dedent);
        }
        else if (pendingIndentation > 0)
        {
            pendingIndentation--;
            return createValuedToken(TokenType.Indent);
        }

        return null;
    }

    private Token? skipAndTryWhitespace()
    {
        while (nextChar == ' ' || nextChar == '\t' || nextChar == '\f')
            moveNext();

        // TODO: probably need to separate types of whitespace.
        if (saveTrivia && currentPos != startPos)
            return createValuedToken(TokenType.WhiteSpace);

        return null;
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
                return createValuedToken(TokenType.Comment);
            }
        }

        return null;
    }

    private Token? tryEof()
    {
        if (nextChar == eof)
        {
            // If indentation stack is not empty, enqueue dedent and return it.
            if (indentStack.Count > 1)
            {
                enqueueDedent(0, 0);
                return tryIndentation();
            }

            // Request stop and return EOF.
            ShouldStop = true;
            return createEmptyToken(TokenType.EndOfFile);
        }

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
                    if (isInvalidStringPrefixes(sawB, sawR, sawU, sawF, sawT) is string errMsg)
                    {
                        if (!sawF && !sawT)
                            // If it's not partial string, read string and put it in error token.
                            return readString(errMsg);
                        else
                            throw new NotImplementedException();
                    }

                    if (sawF || sawT)
                        return readPartialStringStart(sawF ? PartialStringType.FormatString : PartialStringType.TemplateString, sawR);

                    return readString();
                }
            }

            while (isPotentialNameChar(nextChar))
                moveNext();

            return createValuedToken(TokenType.Name);
        }

        return null;
    }

    private Token? tryLineFeed()
    {
        if (nextChar == '\n')
        {
            atLineBeginning = true;
            bool increaseLine = moveNext();
            Token tok;

            if (isBlankLine || bracketsLevel > 0 || atContinuedLine)
            {
                // If line is empty, try to save trivia new line.
                if (saveTrivia)
                {
                    if (isBlankLineWithComment)
                        isBlankLineWithComment = false;
                    tok = createValuedToken(TokenType.TriviaNewLine);
                }
                // Or force reading next token (advance line before it).
                else
                {
                    if (increaseLine)
                        advanceLine();
                    return ReadNext();
                }
            }
            // If line is empty but with comment save trivia.
            else if (isBlankLineWithComment && saveTrivia)
            {
                isBlankLineWithComment = false;
                tok = createValuedToken(TokenType.TriviaNewLine);
            }
            // If we have valued new line
            else
                tok = createValuedToken(TokenType.NewLine);

            if (increaseLine)
                advanceLine();

            return tok;
        }

        return null;
    }

    private void advanceLine()
    {
        lineNumber++;
        currentColumnOffset = 0;
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
                return createValuedToken(TokenType.Ellipsis);
            }

            return createValuedToken(TokenType.Dot);
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
                    moveNext();
                    do
                    {
                        if (nextChar == '_')
                        {
                            if (!char.IsAsciiHexDigit(nextNextChar))
                                return errorInvalidNumber(NumberKind.Hexadecimal);
                        }

                        do
                            moveNext();
                        while (char.IsAsciiHexDigit(nextChar));
                    }
                    while (nextChar == '_');

                    if (isInvalidEndOfNumber(nextChar))
                        return errorInvalidNumber(NumberKind.Hexadecimal);
                }
                // Octal.
                else if (char.ToLower(nextChar) == 'o')
                {
                    moveNext();
                    do
                    {
                        if (nextChar == '_')
                        {
                            if (!isAsciiOctDigit(nextNextChar))
                                return errorInvalidNumber(NumberKind.Octal);
                        }

                        do
                            moveNext();
                        while (isAsciiOctDigit(nextChar));
                    }
                    while (nextChar == '_');

                    if (isInvalidEndOfNumber(nextChar))
                        return errorInvalidNumber(NumberKind.Octal);
                }
                // Binary.
                else if (char.ToLower(nextChar) == 'b')
                {
                    moveNext();
                    do
                    {
                        if (nextChar == '_')
                        {
                            if (!isAsciiBinDigit(nextNextChar))
                                return errorInvalidNumber(NumberKind.Binary);
                        }

                        do
                            moveNext();
                        while (isAsciiBinDigit(nextChar));
                    }
                    while (nextChar == '_');

                    if (isInvalidEndOfNumber(nextChar))
                        return errorInvalidNumber(NumberKind.Binary);
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
                                return errorInvalidNumber(NumberKind.Decimal);
                        }
                    }

                    if (char.IsAsciiDigit(nextChar))
                    {
                        nonZeros = true;
                        bool ok = moveWhileDecimal();
                        if (!ok)
                            return errorInvalidNumber(NumberKind.Decimal);
                    }
                    if (nextChar == '.')
                        readDecimalNumber();

                    else if (nonZeros && !saveTrivia)
                        return errorInvalidNumber("""
                        Leading zeros in decimal integer are not permitted; use an '0o' prefix for octal numbers.
                        """);

                    if (isInvalidEndOfNumber(nextChar))
                        return errorInvalidNumber(NumberKind.Decimal);
                }

                return createValuedToken(TokenType.Number);
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
                return errorInvalidNumber(NumberKind.Decimal);
        }

        // Eat fraction part.
        if (nextChar == '.')
        {
            bool ok = moveFractionPart();
            if (!ok)
                return errorInvalidNumber(NumberKind.Decimal);
        }

        // Eat exponent part.
        if (char.ToLower(nextChar) == 'e')
        {
            bool ok = moveExponentPart();
            if (!ok)
                return errorInvalidNumber(NumberKind.Decimal, true);
        }

        // Eat imaginary part (just one symbol).
        if (char.ToLower(nextChar) == 'j')
        {
            moveNext();

            if (isInvalidEndOfNumber(nextChar))
                return errorInvalidNumber(NumberKind.Imaginary);
        }
        else
        {
            if (isInvalidEndOfNumber(nextChar))
                return errorInvalidNumber(NumberKind.Decimal);
        }

        return createValuedToken(TokenType.Number);
    }

    private enum NumberKind
    {
        Decimal,
        Imaginary,
        Hexadecimal,
        Octal,
        Binary,
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

    /// <summary>
    /// Reads character while they are ASCII letters or underscores.
    /// If last letter is 'j' and <paramref name="kind"/> is <see cref="NumberKind.Decimal"/>
    /// <paramref name="kind"/> would be changed to <see cref="NumberKind.Imaginary"/>.
    /// </summary>
    /// <param name="kind">Kind of number that was trying to read.</param>
    /// <returns>Error token with <see cref="TokenizerError.InvalidLiteral"/> type.</returns>
    private Token errorInvalidNumber(NumberKind kind, bool sawE = false)
    {
        bool lastJ = false;
        bool sawPlusOrMinus = false;
        while (isCharToEatIfInvalidNumber(nextChar) ||
            !sawPlusOrMinus && sawE && (nextChar == '+' || nextChar == '-'))
        {
            if (char.ToLower(nextChar) == 'e')
                sawE = true;
            lastJ = char.ToLower(nextChar) == 'j';
            sawPlusOrMinus = nextChar == '+' || nextChar == '-';

            moveNext();
        }

        if (kind is NumberKind.Decimal && lastJ)
            kind = NumberKind.Imaginary;

        string message = kind switch
        {
            NumberKind.Decimal => invalid_dec,
            NumberKind.Imaginary => invalid_imaginary,
            NumberKind.Hexadecimal => invalid_hex,
            NumberKind.Octal => invalid_oct,
            NumberKind.Binary => invalid_bin,
            _ => throw new UnreachableException(),
        };

        return valuedErrorToken(TokenizerError.InvalidLiteral, message);
    }

    /// <summary>
    /// Reads character while they are ASCII letters or underscores.
    /// </summary>
    /// <param name="message">Message that will be set to <see cref="ErrorMessage"/>.</param>
    /// <returns>Error token with <see cref="TokenizerError.InvalidLiteral"/> type.</returns>
    private Token errorInvalidNumber(string message)
    {
        while (isCharToEatIfInvalidNumber(nextChar))
            moveNext();

        return valuedErrorToken(TokenizerError.InvalidLiteral, message);
    }

    // Eat all characters, that can be interpreted as part of number and ASCII letters except plus and minus.
    private static bool isCharToEatIfInvalidNumber(char ch) =>
        char.IsAsciiLetter(ch) || char.IsDigit(ch) || ch == '.' || ch == '_';

    private static bool isQuote(char ch) => ch == '"' || ch == '\'';

    private Token? tryString() => isQuote(nextChar) ? readString() : null;

    private Token readPartialStringStart(PartialStringType stringType, bool prefixR) => throw new NotImplementedException();

    private Token readString(string? prefixErrMsg = null)
    {
        Debug.Assert(isQuote(nextChar));

        char quote = nextChar;
        int quotesCount = 1;
        int closingQuotesCount = 0;
        bool hasEscapedQuote = false;

        startLineNumber = lineNumber;

        moveNext();
        if (nextChar == quote)
        {
            moveNext();
            // Triple-quoted string.
            if (nextChar == quote)
            {
                moveNext();
                quotesCount = 3;
            }
            // Empty string found.
            else
                closingQuotesCount = 1;
        }

        while (closingQuotesCount != quotesCount)
        {
            if (nextChar == eof || (quotesCount == 1 && nextChar == '\n'))
            {
                string message = "Unterminated string literal.";
                if (hasEscapedQuote)
                    message += " Perhaps you escaped the end quote?";

                return valuedErrorToken(TokenizerError.InvalidLiteral, message, useStartLine: true);

                // TODO: Partial strings.
            }
            if (nextChar == quote)
                closingQuotesCount++;

            else
            {
                closingQuotesCount = 0;
                if (nextChar == '\\')
                {
                    moveNext();
                    if (nextChar == quote)
                        hasEscapedQuote = true;
                }
            }
            bool newLine = moveNext();
            if (newLine)
                advanceLine();
        }

        if (prefixErrMsg is string msg)
            return valuedErrorToken(TokenizerError.InvalidLiteral, msg);

        return createValuedToken(TokenType.StringLiteral, useStartLine: true);
    }

    private Token? tryLineContinuation()
    {
        if (nextChar == '\\')
        {
            return readLineContinuation();
        }

        return null;
    }

    private Token readLineContinuation()
    {
        Debug.Assert(nextChar == '\\');

        moveNext();

        if (nextChar != '\n')
        {
            if (nextChar == eof)
                return emptyErrorToken(TokenizerError.InvalidLineContinuation, "Expected new line.");
            else
                return emptyErrorToken(TokenizerError.InvalidLineContinuation, "Any characters is not allowed after explicit line continuation.");
        }

        atContinuedLine = true;

        if (saveTrivia)
            return createValuedToken(TokenType.BackSlash);

        return ReadNext();
    }

    private Token readOperatorOrErrorToken()
    {
        char prevChar;
        if (opTwoChars(prevChar = nextChar, nextNextChar) is TokenType tok2Type)
        {
            moveNext();
            if (opThreeChars(prevChar, nextChar, nextNextChar) is TokenType tok3Type)
            {
                moveNext();
                moveNext();
                return createValuedToken(tok3Type);
            }

            moveNext();
            return createValuedToken(tok2Type);
        }

        switch (nextChar)
        {
            case '{':
            case '(':
            case '[':
                // Opening brackets.
                bracketsLevel++;
                break;
            case '}':
            case ')':
            case ']':
                // Closing brackets.
                bracketsLevel--;
                break;
        }

        TokenType? tok = opOneChar(nextChar);
        moveNext();
        return tok is TokenType tok1Type ? createValuedToken(tok1Type) : valuedErrorToken(TokenizerError.CharacterError, "Unknown symbol.");
    }

    #region Helpers

    private bool isEof(int position, int offset) => position + offset >= source.Length;

    private bool skipNextCrlf = false;

    /// <summary>
    /// Moves current position to next character in the source.
    /// </summary>
    /// <returns><see langword="true"/> if needs to increase line number; otherwise <see langword="false"/>.</returns>
    private bool moveNext()
    {
        if (isEof(currentPos, 0))
        {
            ShouldStop = true;
            return false;
        }

        // If currently pointed char is LF, we need to increase line number.
        bool increaseLine = lookAtRaw(currentPos, 0) == '\n' || lookAtRaw(currentPos, 0) == '\r';

        currentPos++;
        currentColumnOffset++;

        if (skipNextCrlf)
        {
            currentPos++;
            currentColumnOffset++;
            skipNextCrlf = false;
        }

        // If new pointed char is CRLF skip CR and remain LF.
        if (lookAtRaw(currentPos, 0) == '\r' && lookAtRaw(currentPos, 1) == '\n')
            skipNextCrlf = true;

        // Signal caller that need to increase line number.
        return increaseLine;
    }

    private char lookAt(int position, int offset)
    {
        char ch = lookAtRaw(position, offset);

        return ch == '\r' ? '\n' : ch;
    }

    private char lookAtRaw(int position, int offset) => !isEof(position, offset) ? source[position + offset] : eof;

    private Token createToken(TokenType type, bool emptyLexeme = false, bool useStartLine = false)
    {
        int startLine;
        if (useStartLine)
            startLine = startLineNumber;
        else
            startLine = lineNumber;

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

    private Token createEmptyToken(TokenType type, bool useStartLine = false) => createToken(type, true, useStartLine);

    private Token createValuedToken(TokenType type, bool useStartLine = false) => createToken(type, false, useStartLine);

    private Token emptyErrorToken(TokenizerError error, string message, bool useStartLine = false)
    {
        setError(error, message);
        return createEmptyToken(TokenType.Error, useStartLine);
    }

    private Token valuedErrorToken(TokenizerError error, string message, bool useStartLine = false)
    {
        setError(error, message);
        return createValuedToken(TokenType.Error, useStartLine);
    }

    private void setError(TokenizerError error, string message)
    {
        Error = error;
        ErrorMessage = message;
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

    private static string? isInvalidStringPrefixes(bool b, bool r, bool u, bool f, bool t)
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

    private static bool isAsciiOctDigit(char character) =>
        character >= '0' && character <= '7';

    private static bool isAsciiBinDigit(char character) =>
        character == '1' || character == '0';

    private static TokenType? opOneChar(char ch) => ch switch
    {
        '(' => TokenType.LeftParen,
        ')' => TokenType.RightParen,
        '[' => TokenType.LeftSquareBracket,
        ']' => TokenType.RightSquareBracket,
        '{' => TokenType.LeftBrace,
        '}' => TokenType.RightBrace,
        '.' => TokenType.Dot,
        ',' => TokenType.Comma,
        ':' => TokenType.Colon,
        ';' => TokenType.Semicolon,
        '=' => TokenType.Equal,
        '+' => TokenType.Plus,
        '-' => TokenType.Minus,
        '*' => TokenType.Star,
        '/' => TokenType.Slash,
        '%' => TokenType.Percent,
        '&' => TokenType.Ampersand,
        '|' => TokenType.VertBar,
        '@' => TokenType.At,
        '^' => TokenType.Circumflex,
        '~' => TokenType.Tilde,
        '>' => TokenType.Greater,
        '<' => TokenType.Less,
        '!' => TokenType.Exclamation,
        _ => null,
    };

    private static TokenType? opTwoChars(char ch1, char ch2) => (ch1, ch2) switch
    {
        (':', '=') => TokenType.ColonEqual,
        ('=', '=') => TokenType.EqEqual,
        ('+', '=') => TokenType.PlusEqual,
        ('-', '=') => TokenType.MinusEqual,
        ('-', '>') => TokenType.RightArrow,
        ('*', '=') => TokenType.StarEqual,
        ('*', '*') => TokenType.DoubleStar,
        ('/', '=') => TokenType.SlashEqual,
        ('/', '/') => TokenType.DoubleSlash,
        ('%', '=') => TokenType.PercentEqual,
        ('&', '=') => TokenType.AmpersandEqual,
        ('|', '=') => TokenType.VertBarEqual,
        ('@', '=') => TokenType.AtEqual,
        ('^', '=') => TokenType.CircumflexEqual,
        ('>', '=') => TokenType.GreaterEqual,
        ('>', '>') => TokenType.RightShift,
        ('<', '=') => TokenType.LessEqual,
        ('<', '<') => TokenType.LeftShift,
        ('!', '=') => TokenType.NotEqual,
        _ => null,
    };

    private static TokenType? opThreeChars(char ch1, char ch2, char ch3) => (ch1, ch2, ch3) switch
    {
        ('.', '.', '.') => TokenType.Ellipsis,
        ('*', '*', '=') => TokenType.DoubleStarEqual,
        ('/', '/', '=') => TokenType.DoubleSlashEqual,
        ('>', '>', '=') => TokenType.RightShiftEqual,
        ('<', '<', '=') => TokenType.LeftShiftEqual,
        _ => null,
    };

    #endregion
}
