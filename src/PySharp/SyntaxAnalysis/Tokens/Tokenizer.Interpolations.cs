using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.SyntaxAnalysis.Tokens;

public partial class Tokenizer
{
    private Tokenizer? interpolationNestedTokenizer;
    private readonly char interpolationQuote;
    private readonly int interpolationQuoteCount;
    private readonly int interpolationCurrentGeneration;
    private readonly StringType interpolationStringType;
    private InterpolationTokenizerMode tokenizerMode;
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
    private const int maximum_interpolated_strings_nesting = 5;

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

        // Setup interpolated string stuff.
        interpolationNestedTokenizer = null;
        interpolationStringType = stringType;
        interpolationQuote = quote;
        interpolationQuoteCount = quoteCount;
        interpolationCurrentGeneration = oldGeneration + 1;
        tokenizerMode = InterpolationTokenizerMode.MiddleString;
        pendingErrorTokens = new(1);
    }

    private bool tryInterpolated([NotNullWhen(true)] out Token? token)
    {
        if (interpolationNestedTokenizer is not null)
        {
            // Close nested if it want that.
            if (interpolationNestedTokenizer.ShouldStop)
            {
                killNested();
            }
            else
            {
                bool isNestedParsed = interpolationNestedTokenizer.tryNextInterpolationMode(out token);

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
                    Error = interpolationNestedTokenizer.Error;
                    ErrorMessage = interpolationNestedTokenizer.ErrorMessage;
                }
                return true;
            }
        }

        token = null;
        return false;

        void killNested()
        {
            ReSync(interpolationNestedTokenizer.Synchronize());
            if (interpolationNestedTokenizer.unrecoverable)
                unrecoverable = true;
            interpolationNestedTokenizer = null;
        }
    }

    private void readInterpolatedStringStart(ReadOnlySpan<char> span, [NotNull] out Token? token, StringType stringType)
    {
        if (assertCanReadInterpolated(out token))
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

            interpolationNestedTokenizer = new Tokenizer(Synchronize(), limitInterpolationLines, interpolationCurrentGeneration, stringType, quote, quoteCount);
        }
        else
            throw new ArgumentException("Given source does not contains valid string start.");
    }

    private bool tryNextInterpolationMode([NotNullWhen(true)] out Token? token)
    {
        if (pendingErrorTokens.TryDequeue(out var err))
        {
            token = err;
            return true;
        }

        var span = Source.Span;

        if (tokenizerMode is InterpolationTokenizerMode.MiddleString)
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
                    CreateToken(out token, getMiddle(interpolationStringType));
                    Advance(span);
                    return true;
                }

                // Else we need to switch to regular mode.
                expressionStartBraceLevel++;
                tokenizerMode = InterpolationTokenizerMode.Regular;
                setExprStartLine();

                if (!isAdvanced)
                    return tryNextInterpolationMode(out token);

                CreateToken(out token, getMiddle(interpolationStringType));
                return true;
            }
            else if (NextChar == '}')
            {
                // In format spec shielded braces aren't allowed.
                if (formatSpec)
                {
                    tokenizerMode = InterpolationTokenizerMode.Regular;
                    setExprStartLine();
                    if (isAdvanced)
                    {
                        CreateToken(out token, getMiddle(interpolationStringType));
                        return true;
                    }
                    else
                        return tryNextInterpolationMode(out token);
                }
                // In other we expecting shielded brace.
                if (TwoNextChar != '}')
                {
                    if (isAdvanced)
                    {
                        // Create with valid part.
                        CreateToken(out token, getMiddle(interpolationStringType));

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
                    CreateToken(out token, getMiddle(interpolationStringType));
                    Advance(span);
                    return true;
                }
            }
            // Skip any escaped character.
            else if (NextChar == '\\')
            {
                Advance(span);
            }
            else if (!formatSpec && (NextChar == Eof || interpolationQuoteCount == 1 && NextChar == '\n'))
            {
                ShouldStop = unrecoverable = true;
                ErrorToken(out token, TokenizerError.InvalidLiteral, unterminated_string_message);
                return true;
            }
            // If NextChar is quote, check that we can end string.
            // In format spec we are not done yet.
            else if (NextChar == interpolationQuote && !formatSpec)
            {
                bool isEndReached = (interpolationQuoteCount == 1 && NextChar == interpolationQuote) ||
                                    (interpolationQuoteCount == 3 && NextChar == interpolationQuote
                                                            && TwoNextChar == interpolationQuote
                                                            && ThreeNextChar == interpolationQuote);

                if (isEndReached)
                {
                    // In next iteration we will point to the same symbols, but not advanced...
                    if (isAdvanced)
                    {
                        CreateToken(out token, getMiddle(interpolationStringType));
                        return true;
                    }

                    // ...so we create end token.
                    else
                    {
                        for (int i = 0; i < interpolationQuoteCount; i++)
                            Advance(span);

                        ShouldStop = true;
                        {
                            CreateToken(out token, getEnd(interpolationStringType));
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
        if (tryInterpolated(out token))
            return true;

        ResetStart();

        // If at top of the expression and next is control char update Interpolated string fields.
        if (isInterpolationStringPunctuation(NextChar))
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
                    tokenizerMode = InterpolationTokenizerMode.MiddleString;
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
                tokenizerMode = InterpolationTokenizerMode.MiddleString;
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
                TokenizerError.TooLongInterpolationExpression,
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
                    TokenizerError.UnclosedInterpolationExpression,
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

    private bool assertCanReadInterpolated([NotNullWhen(true)] out Token? token)
    {
        if (interpolationCurrentGeneration + 1 > maximum_interpolated_strings_nesting)
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
                interpolationStringType == StringType.Format ? 'f' : 't',
                maximum_interpolated_strings_nesting);

            ErrorToken(out token, TokenizerError.InterpolatedStringNestingOverflow, message);
            return true;
        }

        token = null;
        return false;
    }

    private readonly record struct InterpolationModeInfo
    {
    }

    private enum InterpolationTokenizerMode
    {
        Regular,
        MiddleString,
    }


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

    private static bool isInterpolationStringPunctuation(char ch) =>
        ch == ':' || ch == '!' || ch == '{' || ch == '}';
}
