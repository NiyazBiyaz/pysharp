using System.Buffers;
using System.Diagnostics;
using PySharp.SyntaxAnalysis.Tokens;

namespace PySharp.SyntaxAnalysis.Common;

/// <summary>
/// Class to parse <see cref="Token.Lexeme"/> with type <see cref="TokenType.StringLiteral"/>
/// into it's value with quotes removed and parsed escape sequences (if no raw specified).
/// </summary>
/// <remarks>
/// Expecting that all parsed string literals are already valid.
/// </remarks>
public static class StringParser
{
    public static string ParseQuotedString(ReadOnlySpan<char> literal)
    {
        Debug.Assert(!HasByte(literal));

        bool hasRaw = false;
        int cursor = 0;

        while (cursor < literal.Length && literal[cursor] != '\'' && literal[cursor] != '"')
        {
            if (literal[cursor] == 'r' || literal[cursor] == 'R')
                hasRaw = true;

            cursor += 1;
        }

        if (cursor == literal.Length)
            return "";

        char quote = literal[cursor];
        int quoteCount = 0;
        while (literal[cursor] == quote)
        {
            cursor += 1;
            quoteCount += 1;
        }

        var workingSlice = literal[cursor..^quoteCount];
        string result;

        bool needNormalizeLineFeed = quoteCount == 3 && literal.Contains('\r');
        bool hasEscaped = literal.Contains('\\');
        bool hasEscapedNewLine = hasEscaped && (
            literal.Contains("\\\n", StringComparison.Ordinal) ||
            literal.Contains("\\\r\f", StringComparison.Ordinal) ||
            literal.Contains("\\\r", StringComparison.Ordinal));
        bool canBeInlined = !needNormalizeLineFeed && (!hasEscaped || hasRaw && !hasEscapedNewLine);

        if (canBeInlined)
            result = workingSlice.ToString();

        else if (hasRaw)
            result = parseLiteralPartRaw(workingSlice);

        else
            result = parseLiteralPartNormal(workingSlice);

        return result;
    }

    private static string parseLiteralPartNormal(ReadOnlySpan<char> literal)
    {
        var pool = ArrayPool<char>.Shared;
        var target = pool.Rent(literal.Length);
        try
        {
            int write = 0;
            for (int read = 0; read < literal.Length; read++)
            {
                if (literal[read] == '\\')
                    read += processEscapeSequence(literal[read..], target, ref write);

                if (read < literal.Length && literal[read] == '\r')
                {
                    target[write++] = '\n';
                    if (read + 1 < literal.Length && literal[read + 1] == '\n')
                        read += 1;
                }
                else
                    target[write++] = literal[read];
            }

            return new string(target.AsSpan()[..write]);
        }
        finally
        {
            pool.Return(target);
        }
    }

    private static int processEscapeSequence(ReadOnlySpan<char> source, Span<char> target, ref int write)
    {
        Debug.Assert(source[0] == '\\');

        int read = 1;

        if (read >= source.Length)
            return 1;

        switch (source[read])
        {
            case 'n':
            {
                target[write++] = '\n';
                break;
            }
            case 'r':
            {
                target[write++] = '\r';
                break;
            }
            case 't':
            {
                target[write++] = '\t';
                break;
            }
            case 'b':
            {
                target[write++] = '\b';
                break;
            }
            case '0':
            {
                target[write++] = '\0';
                break;
            }
            case '\\':
            {
                target[write++] = '\\';
                break;
            }
            case '\"':
            {
                target[write++] = '\"';
                break;
            }
            case '\'':
            {
                target[write++] = '\'';
                break;
            }
            case 'f':
            {
                target[write++] = '\f';
                break;
            }
            case 'v':
            {
                target[write++] = '\v';
                break;
            }
            case 'a':
            {
                target[write++] = '\a';
                break;
            }
            case 'x':
            {
                throw new NotImplementedException();
            }
            case 'u':
            {
                throw new NotImplementedException();
            }
            case 'U':
            {
                throw new NotImplementedException();
            }
            default:
            {
                if (char.IsBetween(source[read], '0', '7'))
                {
                    throw new NotImplementedException();
                }

                // Single character line feed '\r' and '\n' will be skipped below.
                if (read + 1 < source.Length && source[read] == '\r' && source[read + 1] == '\n')
                    read += 1;

                break;
            }
        }

        // Every escape sequence skips at least one character, so do it once here.
        read += 1;

        return read;
    }

    private static string parseLiteralPartRaw(ReadOnlySpan<char> literal)
    {
        var pool = ArrayPool<char>.Shared;
        var arr = pool.Rent(literal.Length);
        try
        {
            int write = 0;
            for (int read = 0; read < literal.Length; read++)
            {
                if (read + 2 < literal.Length && literal[read] == '\\' && literal[read + 1] == '\r' && literal[read + 2] == '\n')
                    read += 3;

                else if (read + 1 < literal.Length && literal[read] == '\\' && (literal[read + 1] == '\n' || literal[read + 1] == '\r'))
                    read += 2;

                // Normalize '\r' and '\r\n' to '\n'.
                if (literal[read] == '\r')
                {
                    arr[write++] = '\n';
                    if (read + 1 < literal.Length && literal[read + 1] == '\n')
                        read += 1;
                }
                else
                    arr[write++] = literal[read];
            }

            return new string(arr.AsSpan()[0..write]);
        }
        finally
        {
            pool.Return(arr);
        }
    }

    public static bool HasByte(ReadOnlySpan<char> literal)
    {
        for (int i = 0; i < literal.Length && literal[i] != '\'' && literal[i] != '"'; i++)
        {
            if (literal[i] == 'b' || literal[i] == 'B')
                return true;
        }

        return false;
    }

    public static bool HasPrefix(ReadOnlySpan<char> literal) =>
        literal.Length == 0 || literal[0] != '\'' && literal[0] != '"';
}
