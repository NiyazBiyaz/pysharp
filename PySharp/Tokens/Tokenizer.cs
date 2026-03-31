using System.Diagnostics;

namespace PySharp.Tokens;

public class Tokenizer(SynchronizationPoint syncPoint, bool saveTrivia) : BaseTokenizer(syncPoint, saveTrivia)
{
    private bool atLineBeginning = true;

    private bool isBlankLine = false;
    private bool isBlankLineWithComment = false;
    private bool atContinuedLine = false;

    private readonly Stack<int> indentStack = syncPoint.IndentStack;
    private readonly Stack<int> alternateIndentStack = syncPoint.AltIndentStack;

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

    public Token ReadNext()
    {
        var span = Source.Span;

        ResetStart();
        Token? maybeToken = tryNextLine(span) ?? tryIndentation();
        if (maybeToken is Token token)
            return token;

        ResetStart();
        maybeToken = skipAndTryWhitespace(span);
        if (maybeToken is Token whiteSpace)
            return whiteSpace;

        ResetStart();
        maybeToken = tryComment(span);
        if (maybeToken is Token comment)
            return comment;

        ResetStart();
        Token definitelyToken =
            tryEof() ??
            tryName(span) ??
            tryLineFeed(span) ??
            tryDotOrFraction(span) ??
            tryNumber(span) ??
            tryString(span) ??
            tryLineContinuation(span) ??
            readOperatorOrErrorToken(span);

        return definitelyToken;
    }

    private Token? tryNextLine(ReadOnlySpan<char> span)
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
                if (NextChar == ' ' || NextChar == '\f')
                {
                    column++;
                    alternateColumn++;
                    Advance(span);
                }
                else if (NextChar == '\t')
                {
                    column = (column / tab_size + 1) * tab_size;
                    alternateColumn = (alternateColumn / alter_tab_size + 1) * alter_tab_size;
                    Advance(span);
                }
                else if (NextChar == '\\')
                {
                    continuationColumn = continuationColumn != 0 ? continuationColumn : column;
                    var lineCont = readLineContinuation(span);
                    if (lineCont.Type is TokenType.Error)
                        return lineCont;
                }
                else
                    break;
            }

            isBlankLine = NextChar == '#' || NextChar == '\n';

            if (!isBlankLine)
            {
                column = continuationColumn == 0 ? column : continuationColumn;
                alternateColumn = continuationColumn == 0 ? alternateColumn : continuationColumn;

                if (column == indentStack.Peek())
                {
                    if (alternateColumn != alternateIndentStack.Peek())
                        return ErrorToken(TokenizerError.IndentationError, tab_space_err_msg, true);
                }
                else if (column > indentStack.Peek())
                {
                    if (alternateColumn <= alternateIndentStack.Peek())
                        return ErrorToken(TokenizerError.IndentationError, tab_space_err_msg, true);

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
        {
            pendingIndentation = 0;
            return ErrorToken(TokenizerError.IndentationError, "Can dedent only on existing indentation level.", true);
        }

        if (alternateColumn != alternateIndentStack.Peek())
        {
            pendingIndentation = 0;
            return ErrorToken(TokenizerError.IndentationError, tab_space_err_msg, true);
        }

        return null;
    }

    private Token? tryIndentation()
    {
        if (pendingIndentation < 0)
        {
            pendingIndentation++;
            // Dedent tokens are empty with zero-width.
            ResetStart();
            return CreateToken(TokenType.Dedent, true);
        }
        else if (pendingIndentation > 0)
        {
            pendingIndentation--;
            return CreateToken(TokenType.Indent);
        }

        return null;
    }

    private Token? skipAndTryWhitespace(ReadOnlySpan<char> span)
    {
        bool hasWhiteSpace = false;
        while (NextChar == ' ' || NextChar == '\t' || NextChar == '\f')
        {
            hasWhiteSpace = true;
            Advance(span);
        }

        // TODO: probably need to separate types of whitespace.
        if (SaveTrivia && hasWhiteSpace)
            return CreateToken(TokenType.WhiteSpace);

        return null;
    }

    private Token? tryComment(ReadOnlySpan<char> span)
    {
        if (NextChar == '#')
        {
            // Skip while not on the line feed.
            while (NextChar != '\n')
                Advance(span);

            if (SaveTrivia)
            {
                isBlankLineWithComment = isBlankLine;
                return CreateToken(TokenType.Comment);
            }
        }

        return null;
    }

    private Token? tryEof()
    {
        if (NextChar == Eof)
        {
            // If indentation stack is not empty, enqueue dedent and return it.
            if (indentStack.Count > 1)
            {
                enqueueDedent(0, 0);
                return tryIndentation();
            }

            // Request stop and return EOF.
            ShouldStop = true;
            return CreateToken(TokenType.EndOfFile);
        }

        return null;
    }

    private Token? tryName(ReadOnlySpan<char> span)
    {
        if (isPotentialNameStart(NextChar))
        {
            int iters = 0;
            bool sawB = false, sawR = false, sawU = false, sawF = false, sawT = false;
            // Try to read prefixed string.
            while (iters < 2)
            {
                if (!sawB && (NextChar == 'b' || NextChar == 'B'))
                    sawB = true;

                else if (!sawR && (NextChar == 'r' || NextChar == 'R'))
                    sawR = true;

                else if (!sawU && (NextChar == 'u' || NextChar == 'U'))
                    sawU = true;

                else if (!sawF && (NextChar == 'f' || NextChar == 'F'))
                    sawF = true;

                else if (!sawF && (NextChar == 't' || NextChar == 'T'))
                    sawT = true;

                else
                    break;

                Advance(span);
                iters++;

                if (NextChar == '\'' || NextChar == '"')
                {
                    if (isInvalidStringPrefixes(sawB, sawR, sawU, sawF, sawT) is string errMsg)
                    {
                        if (!sawF && !sawT)
                            // If it's not partial string, read string and put it in error token.
                            return readString(span, errMsg);
                        else
                            // TODO: Error recovery
                            throw new NotImplementedException();
                    }

                    if (sawF || sawT)
                        return readPartialStringStart(sawF ? StringType.Format : StringType.Template);

                    return readString(span);
                }
            }

            while (isPotentialNameChar(NextChar))
                Advance(span);

            return CreateToken(TokenType.Name);
        }

        return null;
    }

    private Token? tryLineFeed(ReadOnlySpan<char> span)
    {
        if (NextChar == '\n')
        {
            atLineBeginning = true;
            bool increaseLine = Advance(span);
            Token tok;

            if (isBlankLine || bracketsLevel > 0 || atContinuedLine)
            {
                // If line is empty, try to save trivia new line.
                if (SaveTrivia)
                {
                    if (isBlankLineWithComment)
                        isBlankLineWithComment = false;
                    tok = CreateToken(TokenType.TriviaNewLine);
                }
                // Or force reading next token (advance line before it).
                else
                {
                    if (increaseLine)
                        AdvanceLine();
                    return ReadNext();
                }
            }
            // If line is empty but with comment save trivia.
            else if (isBlankLineWithComment && SaveTrivia)
            {
                isBlankLineWithComment = false;
                tok = CreateToken(TokenType.TriviaNewLine);
            }
            // If we have valued new line
            else
                tok = CreateToken(TokenType.NewLine);

            if (increaseLine)
                AdvanceLine();

            return tok;
        }

        return null;
    }

    private Token? tryDotOrFraction(ReadOnlySpan<char> span)
    {
        if (NextChar == '.')
        {
            if (char.IsAsciiDigit(TwoNextChar))
                return readDecimalNumber(span);

            Advance(span);
            if (NextChar == '.' && TwoNextChar == '.')
            {
                Advance(span);
                Advance(span);
                return CreateToken(TokenType.Ellipsis);
            }

            return CreateToken(TokenType.Dot);
        }

        return null;
    }

    private Token? tryNumber(ReadOnlySpan<char> span)
    {
        if (char.IsAsciiDigit(NextChar))
        {
            if (NextChar == '0')
            {
                Advance(span);
                // Hexadecimal.
                if (char.ToLower(NextChar) == 'x')
                {
                    Advance(span);
                    do
                    {
                        if (NextChar == '_')
                        {
                            if (!char.IsAsciiHexDigit(TwoNextChar))
                                return errorInvalidNumber(span, NumberKind.Hexadecimal);
                        }

                        do
                            Advance(span);
                        while (char.IsAsciiHexDigit(NextChar));
                    }
                    while (NextChar == '_');

                    if (isInvalidEndOfNumber(NextChar))
                        return errorInvalidNumber(span, NumberKind.Hexadecimal);
                }
                // Octal.
                else if (char.ToLower(NextChar) == 'o')
                {
                    Advance(span);
                    do
                    {
                        if (NextChar == '_')
                        {
                            if (!isAsciiOctDigit(TwoNextChar))
                                return errorInvalidNumber(span, NumberKind.Octal);
                        }

                        do
                            Advance(span);
                        while (isAsciiOctDigit(NextChar));
                    }
                    while (NextChar == '_');

                    if (isInvalidEndOfNumber(NextChar))
                        return errorInvalidNumber(span, NumberKind.Octal);
                }
                // Binary.
                else if (char.ToLower(NextChar) == 'b')
                {
                    Advance(span);
                    do
                    {
                        if (NextChar == '_')
                        {
                            if (!isAsciiBinDigit(TwoNextChar))
                                return errorInvalidNumber(span, NumberKind.Binary);
                        }

                        do
                            Advance(span);
                        while (isAsciiBinDigit(NextChar));
                    }
                    while (NextChar == '_');

                    if (isInvalidEndOfNumber(NextChar))
                        return errorInvalidNumber(span, NumberKind.Binary);
                }

                // Decimal literal with leading zeros.
                else
                {
                    bool nonZeros = false;

                    while (NextChar == '0')
                    {
                        Advance(span);

                        if (NextChar == '_')
                        {
                            Advance(span);
                            if (!char.IsAsciiDigit(NextChar))
                                return errorInvalidNumber(span, NumberKind.Decimal);
                        }
                    }

                    if (char.IsAsciiDigit(NextChar))
                    {
                        nonZeros = true;
                        bool ok = moveWhileDecimal(span);
                        if (!ok)
                            return errorInvalidNumber(span, NumberKind.Decimal);
                    }
                    if (NextChar == '.')
                        readDecimalNumber(span);

                    else if (nonZeros && !SaveTrivia)
                        return errorInvalidNumber(span, """
                        Leading zeros in decimal integer are not permitted; use an '0o' prefix for octal numbers.
                        """);

                    if (isInvalidEndOfNumber(NextChar))
                        return errorInvalidNumber(span, NumberKind.Decimal);
                }

                return CreateToken(TokenType.Number);
            }

            else
                return readDecimalNumber(span);
        }

        return null;
    }

    private Token readDecimalNumber(ReadOnlySpan<char> span)
    {
        // Eat integer part.
        {
            bool ok = moveWhileDecimal(span);
            if (!ok)
                return errorInvalidNumber(span, NumberKind.Decimal);
        }

        // Eat fraction part.
        if (NextChar == '.')
        {
            bool ok = moveFractionPart(span);
            if (!ok)
                return errorInvalidNumber(span, NumberKind.Decimal);
        }

        // Eat exponent part.
        if (char.ToLower(NextChar) == 'e')
        {
            bool ok = moveExponentPart(span);
            if (!ok)
                return errorInvalidNumber(span, NumberKind.Decimal, true);
        }

        // Eat imaginary part (just one symbol).
        if (char.ToLower(NextChar) == 'j')
        {
            Advance(span);

            if (isInvalidEndOfNumber(NextChar))
                return errorInvalidNumber(span, NumberKind.Imaginary);
        }
        else
        {
            if (isInvalidEndOfNumber(NextChar))
                return errorInvalidNumber(span, NumberKind.Decimal);
        }

        return CreateToken(TokenType.Number);
    }

    private enum NumberKind
    {
        Decimal,
        Imaginary,
        Hexadecimal,
        Octal,
        Binary,
    }

    private bool moveFractionPart(ReadOnlySpan<char> span)
    {
        // Expecting a dot.
        if (NextChar == '.')
            Advance(span);
        else
            return false;

        bool ok = moveWhileDecimal(span);
        return ok;
    }
    private bool moveExponentPart(ReadOnlySpan<char> span)
    {
        // Expecting a 'e' or 'E'
        if (char.ToLower(NextChar) == 'e')
            Advance(span);
        else
            return false;

        // Eat plus or minus in exponent.
        if (NextChar == '+' || NextChar == '-')
        {
            Advance(span);
            if (!char.IsAsciiDigit(NextChar))
                return false;
        }
        // Invalid if after exponent not plus, minus or number.
        else if (!char.IsAsciiDigit(NextChar))
            return false;

        bool ok = moveWhileDecimal(span);
        return ok;
    }

    /// <summary>
    /// Reads character while they are ASCII letters or underscores.
    /// If last letter is 'j' and <paramref name="kind"/> is <see cref="NumberKind.Decimal"/>
    /// <paramref name="kind"/> would be changed to <see cref="NumberKind.Imaginary"/>.
    /// </summary>
    /// <param name="kind">Kind of number that was trying to read.</param>
    /// <returns>Error token with <see cref="TokenizerError.InvalidLiteral"/> type.</returns>
    private Token errorInvalidNumber(ReadOnlySpan<char> span, NumberKind kind, bool sawE = false)
    {
        bool lastJ = false;
        bool sawPlusOrMinus = false;
        while (isCharToEatIfInvalidNumber(NextChar) ||
            !sawPlusOrMinus && sawE && (NextChar == '+' || NextChar == '-'))
        {
            if (char.ToLower(NextChar) == 'e')
                sawE = true;
            lastJ = char.ToLower(NextChar) == 'j';
            sawPlusOrMinus = NextChar == '+' || NextChar == '-';

            Advance(span);
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

        return ErrorToken(TokenizerError.InvalidLiteral, message);
    }

    /// <summary>
    /// Reads character while they are ASCII letters or underscores.
    /// </summary>
    /// <param name="message">Message that will be set to <see cref="ErrorMessage"/>.</param>
    /// <returns>Error token with <see cref="TokenizerError.InvalidLiteral"/> type.</returns>
    private Token errorInvalidNumber(ReadOnlySpan<char> span, string message)
    {
        while (isCharToEatIfInvalidNumber(NextChar))
            Advance(span);

        return ErrorToken(TokenizerError.InvalidLiteral, message);
    }

    // Eat all characters, that can be interpreted as part of number and ASCII letters except plus and minus.
    private static bool isCharToEatIfInvalidNumber(char ch) =>
        char.IsAsciiLetter(ch) || char.IsDigit(ch) || ch == '.' || ch == '_';

    private static bool isQuote(char ch) => ch == '"' || ch == '\'';

    private Token? tryString(ReadOnlySpan<char> span) => isQuote(NextChar) ? readString(span) : null;

    private Token readPartialStringStart(StringType stringType) => throw new NotImplementedException();

    private Token readString(ReadOnlySpan<char> span, string? prefixErrMsg = null)
    {
        Debug.Assert(isQuote(NextChar));

        char quote = NextChar;
        int quotesCount = 1;
        int closingQuoteCount = 0;
        bool hasEscapedQuote = false;

        Advance(span);
        if (NextChar == quote)
        {
            Advance(span);
            // Triple-quoted string.
            if (NextChar == quote)
            {
                Advance(span);
                quotesCount = 3;
            }
            // Empty string found.
            else
                closingQuoteCount = 1;
        }

        while (closingQuoteCount != quotesCount)
        {
            if (NextChar == Eof || (quotesCount == 1 && NextChar == '\n'))
            {
                string message = UnterminatedStringMessage;
                if (hasEscapedQuote)
                    message += " Perhaps you escaped the end quote?";

                return ErrorToken(TokenizerError.InvalidLiteral, message);
            }
            if (NextChar == quote)
                closingQuoteCount++;

            else
            {
                closingQuoteCount = 0;
                if (NextChar == '\\')
                {
                    Advance(span);
                    if (NextChar == quote)
                        hasEscapedQuote = true;
                }
            }
            bool newLine = Advance(span);
            if (newLine)
                AdvanceLine();
        }

        if (prefixErrMsg is string msg)
            return ErrorToken(TokenizerError.InvalidLiteral, msg);

        return CreateToken(TokenType.StringLiteral);
    }

    private Token? tryLineContinuation(ReadOnlySpan<char> span)
    {
        if (NextChar == '\\')
        {
            return readLineContinuation(span);
        }

        return null;
    }

    private Token readLineContinuation(ReadOnlySpan<char> span)
    {
        Debug.Assert(NextChar == '\\');

        Advance(span);

        if (NextChar != '\n')
        {
            if (NextChar == Eof)
                return ErrorToken(TokenizerError.InvalidLineContinuation, "Expected new line.", true);
            else
                return ErrorToken(TokenizerError.InvalidLineContinuation, "Any characters is not allowed after explicit line continuation.", true);
        }

        atContinuedLine = true;

        if (SaveTrivia)
            return CreateToken(TokenType.BackSlash);

        return ReadNext();
    }

    private Token readOperatorOrErrorToken(ReadOnlySpan<char> span)
    {
        char prevChar;
        if (opTwoChars(prevChar = NextChar, TwoNextChar) is TokenType tok2Type)
        {
            Advance(span);
            if (opThreeChars(prevChar, NextChar, TwoNextChar) is TokenType tok3Type)
            {
                Advance(span);
                Advance(span);
                return CreateToken(tok3Type);
            }

            Advance(span);
            return CreateToken(tok2Type);
        }

        switch (NextChar)
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

        TokenType? tok = opOneChar(NextChar);
        Advance(span);
        return tok is TokenType tok1Type ? CreateToken(tok1Type) : ErrorToken(TokenizerError.CharacterError, $"Unknown symbol: {NextChar}.");
    }

    private bool moveWhileDecimal(ReadOnlySpan<char> span)
    {
        while (true)
        {
            while (char.IsAsciiDigit(NextChar))
                Advance(span);

            if (NextChar != '_')
                return true;

            Advance(span);
            if (!char.IsAsciiDigit(NextChar))
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
}
