namespace PySharp.Tokens;

public record SynchronizationPoint
{
    public required IReadOnlyMemoryBuffer<char> SourceBuffer { get; init; }
    public required Stack<int> IndentStack { get; init; }
    public required Stack<int> AltIndentStack { get; init; }
    public required int StartLine { get; init; }
    public required int StartColumn { get; init; }
    public required int BracketsLevel { get; init; }

    public static SynchronizationPoint ClearPoint(IReadOnlyMemoryBuffer<char> buffer) =>
        new()
        {
            SourceBuffer = buffer,
            IndentStack = new([0]),
            AltIndentStack = new([0]),
            StartLine = 0,
            StartColumn = 0,
            BracketsLevel = 0,
        };
}
