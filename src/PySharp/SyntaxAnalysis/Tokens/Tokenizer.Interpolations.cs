using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.SyntaxAnalysis.Tokens;

public partial class Tokenizer
{
    private readonly Stack<InterpolationModeInfo> interpolationModes = [];

    private readonly Queue<Token?> pendingErrorTokens = [];

    private bool tryInterpolated(ReadOnlySpan<char> span, [NotNullWhen(true)] out Token? token)
    {
        if (pendingErrorTokens.TryDequeue(out token))
        {
#pragma warning disable CS8762 // Parameter must have a non-null value when exiting in some condition.
            return true;
#pragma warning restore CS8762 // Parameter must have a non-null value when exiting in some condition.
        }

        if (interpolationModes.Count > 0)
        {
            switch (interpolationModes.Peek().Mode)
            {
                case InterpolationTokenizerMode.Regular:
                    if (tryInterpolationExpression(span, out token))
                        return true;
                    break;

                case InterpolationTokenizerMode.MiddleString:
                    if (tryInterpolationMiddleString(span, out token))
                        return true;
                    break;
            }
        }

        token = null;
        return false;
    }

    private void readInterpolatedStringStart(ReadOnlySpan<char> span, [NotNull] out Token? token, StringType stringType)
    {
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

            interpolationModes.Push(new InterpolationModeInfo()
            {
                Mode = InterpolationTokenizerMode.MiddleString,
                StringType = stringType,
                QuoteChar = quote,
                QuoteCount = quoteCount,
            });
        }
        else
            throw new ArgumentException("Given source does not contains valid string start.");
    }

    private bool tryNextInterpolationMode([NotNullWhen(true)] out Token? token)
    {
        if (pendingErrorTokens.TryDequeue(out token))
        {
#pragma warning disable CS8762 // Parameter must have a non-null value when exiting in some condition.
            return true;
#pragma warning restore CS8762 // Parameter must have a non-null value when exiting in some condition.
        }

        var span = Source.Span;

        if (interpolationModes.Peek().Mode is InterpolationTokenizerMode.MiddleString)
            return tryInterpolationMiddleString(span, out token);

        else
            return tryInterpolationExpression(span, out token);
    }

    private bool tryInterpolationMiddleString(ReadOnlySpan<char> span, [NotNullWhen(true)] out Token? token)
    {
        var mode = interpolationModes.Peek();

        // If false, next Middle string will not be emitted, because it's empty.
        bool isAdvanced = false;

        ResetStart();
        while (true)
        {
            if (NextChar == '{')
            {
                // Shielded brace is not allowed in format spec.
                if (!mode.FormatSpec && TwoNextChar == '{')
                {
                    // Take first brace, but ignore second.
                    Advance(span);
                    CreateToken(out token, getMiddle(mode.StringType));
                    Advance(span);
                    return true;
                }

                // Else we need to switch to regular mode.
                mode.ExpressionStartBraceLevel++;
                mode.Mode = InterpolationTokenizerMode.Regular;

                if (!isAdvanced)
                    return tryNextInterpolationMode(out token);

                CreateToken(out token, getMiddle(mode.StringType));
                return true;
            }
            else if (NextChar == '}')
            {
                // In format spec shielded braces aren't allowed.
                if (mode.FormatSpec)
                {
                    mode.Mode = InterpolationTokenizerMode.Regular;
                    if (isAdvanced)
                    {
                        CreateToken(out token, getMiddle(mode.StringType));
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
                        CreateToken(out token, getMiddle(mode.StringType));

                        ResetStart();
                        Advance(span);

                        // Create with invalid part.
                        ErrorToken(out var invalid, TokenizerError.InvalidLiteral, double_brackets_message);
                        pendingErrorTokens.Enqueue(invalid);

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
                    CreateToken(out token, getMiddle(mode.StringType));
                    Advance(span);
                    return true;
                }
            }
            // Skip any escaped character.
            else if (NextChar == '\\')
            {
                Advance(span);
            }
            else if (!mode.FormatSpec && (NextChar == Eof || mode.QuoteCount == 1 && NextChar == '\n'))
            {
                ErrorToken(out token, TokenizerError.InvalidLiteral, unterminated_string_message);
                interpolationModes.Clear();
                return true;
            }
            // If NextChar is quote, check that we can end string.
            // In format spec we are not done yet.
            else if (NextChar == mode.QuoteChar && !mode.FormatSpec)
            {
                bool isEndReached = (mode.QuoteCount == 1 && NextChar == mode.QuoteChar) ||
                                    (mode.QuoteCount == 3 && NextChar == mode.QuoteChar
                                                            && TwoNextChar == mode.QuoteChar
                                                            && ThreeNextChar == mode.QuoteChar);

                if (isEndReached)
                {
                    // In next iteration we will point to the same symbols, but not advanced...
                    if (isAdvanced)
                    {
                        CreateToken(out token, getMiddle(mode.StringType));
                        return true;
                    }

                    // ...so we create end token.
                    else
                    {
                        for (int i = 0; i < mode.QuoteCount; i++)
                            Advance(span);

                        CreateToken(out token, getEnd(mode.StringType));
                        interpolationModes.Pop();
                        return true;
                    }
                }
            }

            isAdvanced = true;
            bool shouldNewLine = Advance(span);
            if (shouldNewLine)
                AdvanceLine();
        }
    }

    private bool tryInterpolationExpression(ReadOnlySpan<char> span, [NotNullWhen(true)] out Token? token)
    {
        var mode = interpolationModes.Peek();

        ResetStart();

        // If at top of the expression and next is control char update Interpolated string fields.
        if (isInterpolationStringPunctuation(NextChar))
        {
            // This code block executes before braceLevel incremented by opening brace '{'
            // so for ensuring we are on the level 0 we need to adjust it manually.
            int level = mode.BraceLevel - (NextChar != '{' ? 1 : 0);
            bool atLevelWithMeaningfulPunctuation = (level == 1 && mode.FormatSpec) || level == 0;

            if (atLevelWithMeaningfulPunctuation)
            {
                // Enable format spec string.
                if (NextChar == ':')
                {
                    mode.Mode = InterpolationTokenizerMode.MiddleString;
                    mode.FormatSpec = true;
                }
            }

            // Increase/Decrease level of the braces before mode switching.
            if (NextChar == '{')
                mode.BraceLevel++;

            else if (NextChar == '}')
                mode.BraceLevel--;

            // If we on the same level where we start, go back to MiddleString mode.
            if (mode.BraceLevel == mode.ExpressionStartBraceLevel)
            {
                mode.ExpressionStartBraceLevel--;
                mode.Mode = InterpolationTokenizerMode.MiddleString;
                mode.FormatSpec = mode.ExpressionStartBraceLevel >= 0;
            }
        }

        readNext(span, out token);

        if (token.Value.Type == TokenType.EndOfFile)
        {
            ErrorToken(
                out token,
                TokenizerError.UnclosedInterpolationExpression,
                "Unexpected EOF in multi-line statement.");
            interpolationModes.Clear();
            ShouldStop = false; // Rewrite EOF to return error instead.
            return true;
        }

        return true;
    }

    private class InterpolationModeInfo
    {
        public InterpolationTokenizerMode Mode;
        public int QuoteCount;
        public char QuoteChar;
        public StringType StringType;
        public int BraceLevel = 0;
        public int ExpressionStartBraceLevel = -1;
        public bool FormatSpec = false;
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
