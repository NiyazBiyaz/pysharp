using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace PySharp.SyntaxAnalysis.Tokens;

[DebuggerDisplay("next={NextChar} ln={currentLineNumber} cl={currentColumn} len={currentPos-startPos}")]
public abstract class BaseTokenizer
{
    protected IReadOnlyMemoryBuffer<char> Source = null!;
    protected readonly bool SaveTrivia;

    protected char NextChar { get; private set; }
    protected char TwoNextChar { get; private set; }
    protected char ThreeNextChar { get; private set; }

    protected ReadOnlyMemory<char> BufferFromStart => Source.Memory[startPos..];

    protected int CurrentPos => currentPos;

    public bool ShouldStop { get; protected set; } = false;
    public TokenizerError Error { get; protected set; } = TokenizerError.NoError;
    public string? ErrorMessage { get; protected set; } = null;

    private int currentPos;
    private int startPos = 0;

    private int currentColumn;
    protected int StartColumn { get; private set; }

    private int currentLineNumber;
    protected int StartLineNumber { get; private set; }

    private bool skipNextCrlf = false;

    protected const char Eof = '\0';

    public abstract SynchronizationPoint Synchronize();

    protected void ReSync(SynchronizationPoint syncPoint)
    {
        Source = syncPoint.SourceBuffer;

        StartColumn = syncPoint.StartColumn;
        StartLineNumber = currentLineNumber = syncPoint.StartLine;

        // Initialize current position with -1...
        currentPos = -1;
        currentColumn = syncPoint.StartColumn - 1;

        // ...And set *NextChar properties.
        advance(Source.Span);
    }

    protected BaseTokenizer(SynchronizationPoint sync, bool saveTrivia)
    {
        SaveTrivia = saveTrivia;

        ReSync(sync);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool isEof(ReadOnlySpan<char> span, int offset) => (uint)(currentPos + offset) >= (uint)span.Length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void ResetStart()
    {
        startPos = currentPos;
        StartColumn = currentColumn;
        StartLineNumber = currentLineNumber;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void AdvanceLine()
    {
        currentLineNumber += 1;
        currentColumn = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected Token CreateToken(TokenType type, bool emptyLexeme = false)
    {
        var lexeme = emptyLexeme ? ReadOnlyMemory<char>.Empty
                                 : Source.Memory[startPos..currentPos];

        var endPosition = new TokenPosition()
        {
            Line = currentLineNumber,
            Column = currentColumn,
        };
        var startPosition = new TokenPosition()
        {
            Line = StartLineNumber,
            Column = StartColumn,
        };

        return new(type, lexeme, startPosition, endPosition);
    }

    protected Token ErrorToken(TokenizerError error, string message, bool emptyLexeme = false)
    {
        Error = error;
        ErrorMessage = message;
        return CreateToken(TokenType.Error, emptyLexeme);
    }

    /// <summary>
    /// Moves current position to next character and sets properties <see cref="NextChar"/>,
    /// <see cref="TwoNextChar"/> and <see cref="ThreeNextChar"/>.
    /// </summary>
    /// <param name="sourceSpan">Span to the <see cref="Source"/> to avoid recreating it.</param>
    /// <returns><see langword="true"/> if needs to increase line number; otherwise
    /// <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool Advance(ReadOnlySpan<char> sourceSpan)
    {
        if (isEof(sourceSpan, 0))
        {
            ShouldStop = true;
            return false;
        }

        return advance(sourceSpan);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool advance(ReadOnlySpan<char> sourceSpan)
    {
        char next0, next1, next2;

        bool increaseLine = NextChar == '\n' || NextChar == '\r';

        currentPos++;
        currentColumn++;

        // We cannot skip CRLF as soon as we see it because it breaks indexes of the new line token.
        // So, we need to store flag that we've been see at previous advance iteration to skip 2 chars at once.
        if (skipNextCrlf)
        {
            currentPos++;
            currentColumn++;
            skipNextCrlf = false;
        }

        next0 = lookAt(sourceSpan, 0);
        next1 = lookAt(sourceSpan, 1);
        next2 = lookAt(sourceSpan, 2);

        // Characters should be normalized by to LF.
        // If next sequence is CRLF, enable skip flag and read next 2 chars to 'second' and 'third' chars.
        if (next0 == '\r' && next1 == '\n')
        {
            skipNextCrlf = true;

            next1 = next2;
            next2 = lookAt(sourceSpan, 3);

            // If next two chars is also CRLF, read next to 'third' char.
            if (next1 == '\r' && next2 == '\n')
                next2 = lookAt(sourceSpan, 4);
        }

        NextChar = next0 == '\r' ? '\n' : next0;
        TwoNextChar = next1 == '\r' ? '\n' : next1;
        ThreeNextChar = next2 == '\r' ? '\n' : next2;

        return increaseLine;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private char lookAt(ReadOnlySpan<char> span, int offset) => isEof(span, offset) ? Eof : span[currentPos + offset];
}
