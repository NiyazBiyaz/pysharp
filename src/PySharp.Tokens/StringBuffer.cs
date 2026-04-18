namespace PySharp.Tokens;

public class StringBuffer(string str) : IReadOnlyMemoryBuffer<char>
{
    private readonly string str = str;

    public ReadOnlyMemory<char> Memory => str.AsMemory();

    public ReadOnlySpan<char> Span => str.AsSpan();

    public int Length => str.Length;
}
