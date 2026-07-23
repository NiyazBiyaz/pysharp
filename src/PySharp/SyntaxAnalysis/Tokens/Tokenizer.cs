using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.SyntaxAnalysis.Tokens;

public class Tokenizer : BaseTokenizer, ITokenizer
{
    private bool atLineBeginning;
    private bool isBlankLine = false;
    private bool isBlankLineWithComment = false;
    private bool atContinuedLine = false;

    private readonly Stack<int> indentStack;
    private readonly Stack<int> alternateIndentStack;
    private readonly bool limitInterpolationLines;

    private const int tab_size = 8;
    private const int alter_tab_size = 1;
    private const string invalid_dec_message = "Invalid decimal literal.";
    private const string invalid_img_message = "Invalid imaginary literal.";
    private const string invalid_hex_message = "Invalid hexadecimal literal.";
    private const string invalid_oct_message = "Invalid octal literal.";
    private const string invalid_bin_message = "Invalid binary literal.";
    private const string tab_space_mixing_message = "Tabs and spaces mixing is not allowed.";
    private const string double_brackets_message = "Use double curly brackets '}}' to shield it in interpolated string.";
    private const string unterminated_string_message = "Unterminated string literal.";

    private int pendingIndentation
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 1);
            field = value;
        }
    } = 0;
    private int bracketsLevel { get; set; }

    // Partial strings stuff.
    private Tokenizer? partialNestedTokenizer;
    private readonly char partialQuote;
    private readonly int partialQuoteCount;
    private readonly int partialCurrentGeneration;
    private readonly StringType partialStringType;
    private PartialTokenizerMode tokenizerMode;
    private bool unrecoverable = false;
    private int braceLevel = 0;
    private int expressionStartBraceLevel = -1;
    private int expressionStartLine;
    private bool formatSpec = false;
    private readonly Queue<Token> pendingErrorTokens = null!;

    /// <summary>
    /// Maximum lines for one interpolation. It limited by the UX reasons, because in IDE
    /// if user somehow breaks closing brackets he would to see all file red of errors.
    /// This constant prevents such scenarios and saves error locality.
    /// Another reason for it is the fact, that such a long interpolations are ugly.
    /// </summary>
    // TODO: Now we just do opposite thing and read all buffer as error. Needs something like
    // signal to buffer to limit itself by some safe-point that doesn't was changed, but later.
    private const int interpolation_maximum_lines = 4;
    /// <summary>
    /// Maximum F-/T-strings that can be nested to each other by the Python reference
    ///
    /// <see href="https://docs.python.org/3/reference/lexical_analysis.html#formatted-string-literals"/>
    /// </summary>
    private const int maximum_partial_strings_nesting = 5;

    public Tokenizer(SynchronizationPoint syncPoint, bool limitInterpolationLines = true)
        : base(syncPoint)
    {
        indentStack = syncPoint.IndentStack;
        alternateIndentStack = syncPoint.AltIndentStack;
        bracketsLevel = syncPoint.BracketsLevel;

        this.limitInterpolationLines = limitInterpolationLines;
        atLineBeginning = true;

        partialNestedTokenizer = null;
        partialQuote = Eof;
        partialQuoteCount = -1;
        partialCurrentGeneration = 0;
    }

    private Tokenizer(SynchronizationPoint syncPoint, bool limitInterpolationLines, int oldGeneration, StringType stringType, char quote, int quoteCount)
        : base(syncPoint)
    {
        // Setup synchronization point.
        indentStack = syncPoint.IndentStack;
        alternateIndentStack = syncPoint.AltIndentStack;
        bracketsLevel = syncPoint.BracketsLevel;

        this.limitInterpolationLines = limitInterpolationLines;
        // Set it false, because in nested tokenizer it can be start of the line (if true, may create invalid dedents)
        atLineBeginning = false;

        // Setup partial string stuff.
        partialNestedTokenizer = null;
        partialStringType = stringType;
        partialQuote = quote;
        partialQuoteCount = quoteCount;
        partialCurrentGeneration = oldGeneration + 1;
        tokenizerMode = PartialTokenizerMode.MiddleString;
        pendingErrorTokens = new(1);
    }

    public override SynchronizationPoint Synchronize()
    {
        ResetStart();
        return new()
        {
            SourceBuffer = new MemoryCharBuffer(BufferFromStart),
            StartLine = StartLineNumber,
            StartColumn = StartColumn,
            BracketsLevel = bracketsLevel,
            IndentStack = indentStack,
            AltIndentStack = alternateIndentStack,
        };
    }

    public void ReadNext([NotNull] out Token? token)
    {
        if (ShouldStop)
            throw new InvalidOperationException("Buffer already was read. Check ShouldStop flag before calling.");

        if (tryPartial(out token))
        {
            return;
        }

        var span = Source.Span;

        ResetStart();

        if (tryNextLine(span, out token) || tryIndentation(out token))
        {
            return;
        }

        ResetStart();
        if (skipAndTryWhitespace(span, out token))
        {
            return;
        }

        ResetStart();
        if (tryComment(span, out token))
        {
            return;
        }

        ResetStart();
        if (tryEof(out token) ||
            tryName(span, out token) ||
            tryLineFeed(span, out token) ||
            tryDotOrFraction(span, out token) ||
            tryNumber(span, out token) ||
            tryString(span, out token) ||
            tryLineContinuation(span, out token))
        {
            return;
        }

        readOperatorOrErrorToken(span, out token);
        return;
    }

    private bool tryPartial([NotNullWhen(true)] out Token? token)
    {
        if (partialNestedTokenizer is not null)
        {
            // Close nested if it want that.
            if (partialNestedTokenizer.ShouldStop)
            {
                killNested();
            }
            else
            {
                bool isNestedParsed = partialNestedTokenizer.tryNextPartialMode(out token);

                // If nested tokenizer returns null, it means that it can't more read the characters
                // but in some weird condition. So we need to delete it now and resume tokenizing.
                if (!isNestedParsed)
                {
                    killNested();
                    token = null;
                    return false;
                }

                // Throw out errors.
                if (token!.Value.Type is TokenType.Error)
                {
                    Error = partialNestedTokenizer.Error;
                    ErrorMessage = partialNestedTokenizer.ErrorMessage;
                }
                return true;
            }
        }

        token = null;
        return false;

        void killNested()
        {
            ReSync(partialNestedTokenizer.Synchronize());
            if (partialNestedTokenizer.unrecoverable)
                unrecoverable = true;
            partialNestedTokenizer = null;
        }
    }

    private bool tryNextLine(ReadOnlySpan<char> span, [NotNullWhen(true)] out Token? token)
    {
        if (atLineBeginning)
        {
            atLineBeginning = false;

            // Remain whitespace if we in brackets or on continued line for trivia whitespace tokens.
            if (bracketsLevel != 0 || atContinuedLine)
            {
                atContinuedLine = false;
                token = null;
                return false;
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
                    readLineContinuation(span, out token);
                    if (token?.Type is TokenType.Error)
                        return true;
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
                    {
                        ErrorToken(out token, TokenizerError.IndentationError, tab_space_mixing_message, true);
                        return true;
                    }

                    if (alternateColumn != 0)
                    {
                        CreateToken(out token, TokenType.WhiteSpace);
                        return true;
                    }

                }
                else if (column > indentStack.Peek())
                {
                    if (alternateColumn <= alternateIndentStack.Peek())
                    {
                        ErrorToken(out token, TokenizerError.IndentationError, tab_space_mixing_message, true);
                        return true;
                    }

                    pendingIndentation++;
                    indentStack.Push(column);
                    alternateIndentStack.Push(alternateColumn);
                }
                else if (column < indentStack.Peek())
                {
                    if (tryEnqueueDedent(column, alternateColumn, out token))
                        return true;
                }
            }
            else if (alternateColumn != 0)
            {
                CreateToken(out token, TokenType.WhiteSpace);
                return true;
            }
        }

        token = null;
        return false;
    }

    /// <summary>
    /// Reduces current indentation stack top element to <paramref name="column"/> and enqueues
    /// <see cref="TokenType.Dedent"/> tokens to <see cref="pendingIndentation"/>.
    /// </summary>
    private bool tryEnqueueDedent(int column, int alternateColumn, [NotNullWhen(true)] out Token? token)
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
            ErrorToken(out token, TokenizerError.IndentationError, "Can dedent only on existing indentation level.", true);
            return true;
        }

        if (alternateColumn != alternateIndentStack.Peek())
        {
            pendingIndentation = 0;
            ErrorToken(out token, TokenizerError.IndentationError, tab_space_mixing_message, true);
            return true;
        }

        token = null;
        return false;
    }

    private bool tryIndentation([NotNullWhen(true)] out Token? token)
    {
        if (pendingIndentation < 0)
        {
            pendingIndentation++;
            // Dedent tokens are empty with zero-width.
            ResetStart();
            CreateToken(out token, TokenType.Dedent, true);
            return true;
        }
        else if (pendingIndentation > 0)
        {
            pendingIndentation--;
            CreateToken(out token, TokenType.Indent);
            return true;
        }

        token = null;
        return false;
    }

    private bool skipAndTryWhitespace(ReadOnlySpan<char> span, [NotNullWhen(true)] out Token? token)
    {
        bool hasWhiteSpace = false;
        while (NextChar == ' ' || NextChar == '\t' || NextChar == '\f')
        {
            hasWhiteSpace = true;
            Advance(span);
        }

        if (hasWhiteSpace)
        {
            CreateToken(out token, TokenType.WhiteSpace);
            return true;
        }

        token = null;
        return false;
    }

    private bool tryComment(ReadOnlySpan<char> span, [NotNullWhen(true)] out Token? token)
    {
        if (NextChar == '#')
        {
            // Skip while not on the line feed.
            while (NextChar != '\n')
                Advance(span);

            isBlankLineWithComment = isBlankLine;
            CreateToken(out token, TokenType.Comment);
            return true;
        }

        token = null;
        return false;
    }

    private bool tryEof([NotNullWhen(true)] out Token? token)
    {
        if (NextChar == Eof)
        {
            // If indentation stack is not empty, enqueue dedent and return it.
            if (indentStack.Count > 1)
            {
                tryEnqueueDedent(0, 0, out _);
                return tryIndentation(out token);
            }

            // Request stop and return EOF.
            ShouldStop = true;
            CreateToken(out token, TokenType.EndOfFile);
            return true;
        }

        token = null;
        return false;
    }

    private bool tryName(ReadOnlySpan<char> span, [NotNullWhen(true)] out Token? token)
    {
        if (isPotentialNameStart(NextChar))
        {
            if (tryPrefixedString(span, out token))
                return true;

            while (isPotentialNameChar(NextChar))
                Advance(span);

            CreateToken(out token, TokenType.Name);
            return true;
        }

        token = null;
        return false;
    }

    private bool tryPrefixedString(ReadOnlySpan<char> span, [NotNullWhen(true)] out Token? token)
    {
        bool sawB, sawR, sawU, sawF, sawT;

        sawB = char.ToLower(NextChar) == 'b';
        sawR = char.ToLower(NextChar) == 'r';
        sawU = char.ToLower(NextChar) == 'u';
        sawF = char.ToLower(NextChar) == 'f';
        sawT = char.ToLower(NextChar) == 't';

        if (!(sawB || sawR || sawU || sawF || sawT))
        {
            token = null;
            return false;
        }

        if (isQuote(TwoNextChar))
        {
            if (sawF || sawT)
            {
                readPartialStringStart(span, out token, sawF ? StringType.Format : StringType.Template);
                return true;
            }

            Advance(span);
            readString(span, out token);
            return true;
        }

        sawB = sawB || char.ToLower(TwoNextChar) == 'b';
        sawR = sawR || char.ToLower(TwoNextChar) == 'r';
        sawU = sawU || char.ToLower(TwoNextChar) == 'u';
        sawF = sawF || char.ToLower(TwoNextChar) == 'f';
        sawT = sawT || char.ToLower(TwoNextChar) == 't';

        if (isQuote(ThreeNextChar) && isPotentialPrefix(TwoNextChar))
        {
            if (isInvalidStringPrefixes(sawB, sawR, sawU, sawF, sawT) is string prefixErrMsg)
            {
                Advance(span);
                Advance(span);
                if (!sawF && !sawT)
                {
                    // If it's not partial string, read string and put it in error token.
                    readString(span, out token, prefixErrMsg);
                    return true;
                }
                else
                {
                    ErrorToken(out token, TokenizerError.InvalidLiteral, prefixErrMsg);
                    return true;
                }
            }
            else
            {
                if (sawF || sawT)
                {
                    readPartialStringStart(span, out token, sawF ? StringType.Format : StringType.Template);
                    return true;
                }

                Advance(span);
                Advance(span);
                readString(span, out token);
                return true;
            }
        }

        token = null;
        return false;

        static bool isPotentialPrefix(char ch) =>
            char.ToLower(ch) == 'r' ||
            char.ToLower(ch) == 'b' ||
            char.ToLower(ch) == 'u' ||
            char.ToLower(ch) == 'f' ||
            char.ToLower(ch) == 't';
    }

    private bool tryLineFeed(ReadOnlySpan<char> span, [NotNullWhen(true)] out Token? token)
    {
        if (NextChar == '\n')
        {
            atLineBeginning = true;
            bool increaseLine = Advance(span);

            if (isBlankLine || bracketsLevel > 0 || atContinuedLine)
            {
                // If line is empty, save trivia new line.
                if (isBlankLineWithComment)
                    isBlankLineWithComment = false;
                CreateToken(out token, TokenType.TriviaNewLine);
            }
            // If line is empty but with comment save trivia.
            else if (isBlankLineWithComment)
            {
                isBlankLineWithComment = false;
                CreateToken(out token, TokenType.TriviaNewLine);
            }
            // If we have valued new line
            else
                CreateToken(out token, TokenType.NewLine);

            if (increaseLine)
                AdvanceLine();

            return true;
        }

        token = null;
        return false;
    }

    private bool tryDotOrFraction(ReadOnlySpan<char> span, [NotNullWhen(true)] out Token? token)
    {
        if (NextChar == '.')
        {
            if (char.IsAsciiDigit(TwoNextChar))
            {
                readDecimalNumber(span, out token);
                return true;
            }

            Advance(span);
            if (NextChar == '.' && TwoNextChar == '.')
            {
                Advance(span);
                Advance(span);
                CreateToken(out token, TokenType.Ellipsis);
            }

            CreateToken(out token, TokenType.Dot);
            return true;
        }

        token = null;
        return false;
    }

    private bool tryNumber(ReadOnlySpan<char> span, [NotNullWhen(true)] out Token? token)
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
                            {
                                errorInvalidNumber(span, out token, NumberKind.Hexadecimal);
                                return true;
                            }
                        }

                        do
                            Advance(span);
                        while (char.IsAsciiHexDigit(NextChar));
                    }
                    while (NextChar == '_');

                    if (isInvalidEndOfNumber(NextChar))
                    {
                        errorInvalidNumber(span, out token, NumberKind.Hexadecimal);
                        return true;
                    }
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
                            {
                                errorInvalidNumber(span, out token, NumberKind.Octal);
                                return true;
                            }
                        }

                        do
                            Advance(span);
                        while (isAsciiOctDigit(NextChar));
                    }
                    while (NextChar == '_');

                    if (isInvalidEndOfNumber(NextChar))
                    {
                        errorInvalidNumber(span, out token, NumberKind.Octal);
                        return true;
                    }
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
                            {
                                errorInvalidNumber(span, out token, NumberKind.Binary);
                                return true;
                            }
                        }

                        do
                            Advance(span);
                        while (isAsciiBinDigit(NextChar));
                    }
                    while (NextChar == '_');

                    if (isInvalidEndOfNumber(NextChar))
                    {
                        errorInvalidNumber(span, out token, NumberKind.Binary);
                        return true;
                    }
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
                            {
                                errorInvalidNumber(span, out token, NumberKind.Decimal);
                                return true;
                            }
                        }
                    }

                    if (char.IsAsciiDigit(NextChar))
                    {
                        nonZeros = true;
                        bool ok = moveWhileDecimal(span);
                        if (!ok)
                        {
                            errorInvalidNumber(span, out token, NumberKind.Decimal);
                            return true;
                        }
                    }
                    if (NextChar == '.')
                    {
                        readDecimalNumber(span, out token);
                        return true;
                    }

                    else if (nonZeros)
                    {
                        errorInvalidNumber(span, out token, """
                        Leading zeros in decimal integer are not permitted; use an '0o' prefix for octal numbers.
                        """);
                        return true;
                    }

                    if (isInvalidEndOfNumber(NextChar))
                    {
                        errorInvalidNumber(span, out token, NumberKind.Decimal);
                        return true;
                    }
                }

                CreateToken(out token, TokenType.Number);
                return true;
            }

            else
            {
                readDecimalNumber(span, out token);
                return true;
            }
        }

        token = null;
        return false;
    }

    private void readDecimalNumber(ReadOnlySpan<char> span, [NotNull] out Token? token)
    {
        // Eat integer part.
        {
            bool ok = moveWhileDecimal(span);
            if (!ok)
            {
                errorInvalidNumber(span, out token, NumberKind.Decimal);
                return;
            }
        }

        // Eat fraction part.
        if (NextChar == '.')
        {
            bool ok = moveFractionPart(span);
            if (!ok)
            {
                errorInvalidNumber(span, out token, NumberKind.Decimal);
                return;
            }
        }

        // Eat exponent part.
        if (char.ToLower(NextChar) == 'e')
        {
            bool ok = moveExponentPart(span);
            if (!ok)
            {
                errorInvalidNumber(span, out token, NumberKind.Decimal, true);
                return;
            }
        }

        // Eat imaginary part (just one symbol).
        if (char.ToLower(NextChar) == 'j')
        {
            Advance(span);

            if (isInvalidEndOfNumber(NextChar))
            {
                errorInvalidNumber(span, out token, NumberKind.Imaginary);
                return;
            }
        }
        else
        {
            if (isInvalidEndOfNumber(NextChar))
            {
                errorInvalidNumber(span, out token, NumberKind.Decimal);
                return;
            }
        }

        CreateToken(out token, TokenType.Number);
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
    private void errorInvalidNumber(ReadOnlySpan<char> span, [NotNull] out Token? token, NumberKind kind, bool sawE = false)
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
            NumberKind.Decimal => invalid_dec_message,
            NumberKind.Imaginary => invalid_img_message,
            NumberKind.Hexadecimal => invalid_hex_message,
            NumberKind.Octal => invalid_oct_message,
            NumberKind.Binary => invalid_bin_message,
            _ => throw new UnreachableException(),
        };

        ErrorToken(out token, TokenizerError.InvalidLiteral, message);
    }

    /// <summary>
    /// Reads character while they are ASCII letters or underscores.
    /// </summary>
    /// <param name="message">Message that will be set to <see cref="ErrorMessage"/>.</param>
    /// <returns>Error token with <see cref="TokenizerError.InvalidLiteral"/> type.</returns>
    private void errorInvalidNumber(ReadOnlySpan<char> span, [NotNull] out Token? token, string message)
    {
        while (isCharToEatIfInvalidNumber(NextChar))
            Advance(span);

        ErrorToken(out token, TokenizerError.InvalidLiteral, message);
    }

    // Eat all characters, that can be interpreted as part of number and ASCII letters except plus and minus.
    private static bool isCharToEatIfInvalidNumber(char ch) =>
        char.IsAsciiLetter(ch) || char.IsDigit(ch) || ch == '.' || ch == '_';

    private static bool isQuote(char ch) => ch == '"' || ch == '\'';

    private bool tryString(ReadOnlySpan<char> span, [NotNullWhen(true)] out Token? token)
    {
        if (isQuote(NextChar))
        {
            readString(span, out token);
            return true;
        }

        token = null;
        return false;
    }

    private void readPartialStringStart(ReadOnlySpan<char> span, [NotNull] out Token? token, StringType stringType)
    {
        if (assertCanReadPartial(out token))
        {
            return;
        }

        Advance(span); // First char always prefix.

        char quote;
        int quoteCount;

        // Second char can be another letter in prefix, so skip it.
        if (NextChar != '\'' && NextChar != '"')
            Advance(span);

        // Expect quote or fail in 'else'
        if (NextChar == '\'' || NextChar == '"')
        {
            quote = NextChar;

            // If next 2 chars also quotes, it's a triple-quoted string.
            if (TwoNextChar == quote && ThreeNextChar == quote)
            {
                Advance(span);
                Advance(span);
                quoteCount = 3;
            }
            else
                quoteCount = 1;

            Advance(span);

            CreateToken(out token, getStart(stringType));

            partialNestedTokenizer = new Tokenizer(Synchronize(), limitInterpolationLines, partialCurrentGeneration, stringType, quote, quoteCount);
        }
        else
            throw new ArgumentException("Given source does not contains valid string start.");
    }

    private void readString(ReadOnlySpan<char> span, [NotNull] out Token? token, string? prefixErrMsg = null)
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
                string message = unterminated_string_message;
                if (hasEscapedQuote)
                    message += " Perhaps you escaped the end quote?";

                if (NextChar == Eof)
                    unrecoverable = true;

                ErrorToken(out token, TokenizerError.InvalidLiteral, message);
                return;
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
            ErrorToken(out token, TokenizerError.InvalidLiteral, msg);

        CreateToken(out token, TokenType.StringLiteral);
    }

    private bool tryLineContinuation(ReadOnlySpan<char> span, [NotNullWhen(true)] out Token? token)
    {
        if (NextChar == '\\')
        {
            readLineContinuation(span, out token);
            return true;
        }

        token = null;
        return false;
    }

    private void readLineContinuation(ReadOnlySpan<char> span, [NotNull] out Token? token)
    {
        Debug.Assert(NextChar == '\\');

        Advance(span);

        if (NextChar != '\n')
        {
            if (NextChar == Eof)
                ErrorToken(out token, TokenizerError.InvalidLineContinuation, "Expected new line.", true);
            else
                ErrorToken(out token, TokenizerError.InvalidLineContinuation, "Any characters is not allowed after explicit line continuation.", true);
            return;
        }

        atContinuedLine = true;

        CreateToken(out token, TokenType.BackSlash);
        return;
    }

    private void readOperatorOrErrorToken(ReadOnlySpan<char> span, [NotNull] out Token? token)
    {
        char prevChar;
        if (opTwoChars(prevChar = NextChar, TwoNextChar) is TokenType tok2Type)
        {
            Advance(span);
            if (opThreeChars(prevChar, NextChar, TwoNextChar) is TokenType tok3Type)
            {
                Advance(span);
                Advance(span);
                CreateToken(out token, tok3Type);
                return;
            }

            Advance(span);
            CreateToken(out token, tok2Type);
            return;
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
        if (tok is TokenType tok1Type)
        {
            CreateToken(out token, tok1Type);
        }
        else
        {
            ErrorToken(out token, TokenizerError.CharacterError, $"Unknown symbol: '{NextChar}'.");
        }
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

    private bool tryNextPartialMode([NotNullWhen(true)] out Token? token)
    {
        if (pendingErrorTokens.TryDequeue(out var err))
        {
            token = err;
            return true;
        }

        var span = Source.Span;

        if (tokenizerMode is PartialTokenizerMode.MiddleString)
            return tryMiddleString(span, out token);

        else
            return tryExpression(span, out token);
    }

    private bool tryMiddleString(ReadOnlySpan<char> span, [NotNullWhen(true)] out Token? token)
    {
        // If false, next Middle string will not be emitted, because it's empty.
        bool isAdvanced = false;

        ResetStart();
        while (true)
        {
            if (NextChar == '{')
            {
                // Shielded brace is not allowed in format spec.
                if (!formatSpec && TwoNextChar == '{')
                {
                    // Take first brace, but ignore second.
                    Advance(span);
                    CreateToken(out token, getMiddle(partialStringType));
                    Advance(span);
                    return true;
                }

                // Else we need to switch to regular mode.
                expressionStartBraceLevel++;
                tokenizerMode = PartialTokenizerMode.Regular;
                setExprStartLine();

                if (!isAdvanced)
                    return tryNextPartialMode(out token);

                CreateToken(out token, getMiddle(partialStringType));
                return true;
            }
            else if (NextChar == '}')
            {
                // In format spec shielded braces aren't allowed.
                if (formatSpec)
                {
                    tokenizerMode = PartialTokenizerMode.Regular;
                    setExprStartLine();
                    if (isAdvanced)
                    {
                        CreateToken(out token, getMiddle(partialStringType));
                        return true;
                    }
                    else
                        return tryNextPartialMode(out token);
                }
                // In other we expecting shielded brace.
                if (TwoNextChar != '}')
                {
                    if (isAdvanced)
                    {
                        // Create with valid part.
                        CreateToken(out token, getMiddle(partialStringType));

                        ResetStart();
                        Advance(span);

                        // Create with invalid part.
                        ErrorToken(out var invalid, TokenizerError.InvalidLiteral, double_brackets_message);
                        pendingErrorTokens.Enqueue(invalid.Value);

                        if (isAdvanced)
                            return true;
                    }
                    else
                    {
                        Advance(span);
                        ErrorToken(out token, TokenizerError.InvalidLiteral, double_brackets_message);
                        return true;
                    }
                }
                // See above.
                else if (TwoNextChar == '}')
                {
                    // Ignore second brace.
                    Advance(span);
                    CreateToken(out token, getMiddle(partialStringType));
                    Advance(span);
                    return true;
                }
            }
            // Skip any escaped character.
            else if (NextChar == '\\')
            {
                Advance(span);
            }
            else if (!formatSpec && (NextChar == Eof || partialQuoteCount == 1 && NextChar == '\n'))
            {
                ShouldStop = unrecoverable = true;
                ErrorToken(out token, TokenizerError.InvalidLiteral, unterminated_string_message);
                return true;
            }
            // If NextChar is quote, check that we can end string.
            // In format spec we are not done yet.
            else if (NextChar == partialQuote && !formatSpec)
            {
                bool isEndReached = (partialQuoteCount == 1 && NextChar == partialQuote) ||
                                    (partialQuoteCount == 3 && NextChar == partialQuote
                                                            && TwoNextChar == partialQuote
                                                            && ThreeNextChar == partialQuote);

                if (isEndReached)
                {
                    // In next iteration we will point to the same symbols, but not advanced...
                    if (isAdvanced)
                    {
                        CreateToken(out token, getMiddle(partialStringType));
                        return true;
                    }

                    // ...so we create end token.
                    else
                    {
                        for (int i = 0; i < partialQuoteCount; i++)
                            Advance(span);

                        ShouldStop = true;
                        {
                            CreateToken(out token, getEnd(partialStringType));
                            return true;
                        }
                    }
                }
            }

            isAdvanced = true;
            bool shouldNewLine = Advance(span);
            if (shouldNewLine)
                AdvanceLine();
        }

        // Set start of the expression to be able manage lines count and create error when limit reached.
        void setExprStartLine() => expressionStartLine = StartLineNumber;
    }

    private bool tryExpression(ReadOnlySpan<char> span, out Token? token)
    {
        // Maybe another nested tokenizer.
        if (tryPartial(out token))
            return true;

        ResetStart();

        // If at top of the expression and next is control char update Partial string fields.
        if (isPartialStringPunctuation(NextChar))
        {
            // This code block executes before braceLevel incremented by opening brace '{'
            // so for ensuring we are on the level 0 we need to adjust it manually.
            int level = braceLevel - (NextChar != '{' ? 1 : 0);
            bool atLevelWithMeaningfulPunctuation = (level == 1 && formatSpec) || level == 0;

            if (atLevelWithMeaningfulPunctuation)
            {
                // Enable format spec string.
                if (NextChar == ':')
                {
                    tokenizerMode = PartialTokenizerMode.MiddleString;
                    formatSpec = true;
                }
            }

            // Increase/Decrease level of the braces before mode switching.
            if (NextChar == '{')
                braceLevel++;

            else if (NextChar == '}')
                braceLevel--;

            // If we on the same level where we start, go back to MiddleString mode.
            if (braceLevel == expressionStartBraceLevel)
            {
                expressionStartBraceLevel--;
                tokenizerMode = PartialTokenizerMode.MiddleString;
                formatSpec = expressionStartBraceLevel >= 0;
            }
        }

        ReadNext(out token);

        // Check the limit of the maximum lines in expression if no compatible mode enabled.
        if (!unrecoverable && limitInterpolationLines && StartLineNumber - expressionStartLine > interpolation_maximum_lines)
        {
            while (NextChar != Eof)
            {
                if (Advance(span))
                    AdvanceLine();
            }

            ShouldStop = true;
            unrecoverable = true;
            ErrorToken(
                out token,
                TokenizerError.PartialTooLongExpression,
                $"Interpolation exceeds maximum line limit. Allowed maximum {interpolation_maximum_lines} lines."
            );
            return true;
        }

        // Nested tokenizers shouldn't return EOF token, only the root one.
        if (token?.Type is TokenType.EndOfFile)
        {
            // If it's not unrecoverable error it's mean that expression is unclosed,
            // so we need to create such error token and in next iteration control will
            // comeback to root tokenizer.
            if (!unrecoverable)
            {
                ErrorToken(
                    out token,
                    TokenizerError.PartialUnclosedExpression,
                    "Unexpected EOF in multi-line statement."
                );
                return true;
            }

            // ShouldStop already set to true.
            token = null;
            return false;
        }

        return true;
    }

    private bool assertCanReadPartial([NotNullWhen(true)] out Token? token)
    {
        if (partialCurrentGeneration + 1 > maximum_partial_strings_nesting)
        {
            var span = Source.Span;

            while (NextChar != Eof)
            {
                if (Advance(span))
                    AdvanceLine();
            }

            ShouldStop = true;
            unrecoverable = true;

            var message = string.Format("{0}-string: nesting depth exceeded (limit: {1}).",
                partialStringType == StringType.Format ? 'f' : 't',
                maximum_partial_strings_nesting);

            ErrorToken(out token, TokenizerError.PartialNestingOverflow, message);
            return true;
        }

        token = null;
        return false;
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
                return string.Format(err_format, 'b', 'f');
            if (t)
                return string.Format(err_format, 'b', 't');
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

    private static TokenType getStart(StringType type) => type switch
    {
        StringType.Format => TokenType.FStringStart,
        StringType.Template => TokenType.TStringStart,
        _ => throw new UnreachableException()
    };
    private static TokenType getMiddle(StringType type) => type switch
    {
        StringType.Format => TokenType.FStringMiddle,
        StringType.Template => TokenType.TStringMiddle,
        _ => throw new UnreachableException()
    };
    private static TokenType getEnd(StringType type) => type switch
    {
        StringType.Format => TokenType.FStringEnd,
        StringType.Template => TokenType.TStringEnd,
        _ => throw new UnreachableException()
    };

    private static bool isPartialStringPunctuation(char ch) =>
        ch == ':' || ch == '!' || ch == '{' || ch == '}';

    private enum PartialTokenizerMode
    {
        Regular,
        MiddleString,
    }
}
