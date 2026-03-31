namespace PySharp.Tokens;

public class MemoryCharBuffer(ReadOnlyMemory<char> buffer) : IReadOnlyMemoryBuffer<char>
{
    private readonly ReadOnlyMemory<char> buffer = buffer;

    public ReadOnlyMemory<char> Memory => buffer;

    public ReadOnlySpan<char> Span => buffer.Span;

    public int Length => buffer.Length;
}
