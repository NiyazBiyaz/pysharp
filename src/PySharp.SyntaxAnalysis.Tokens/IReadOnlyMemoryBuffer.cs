namespace PySharp.SyntaxAnalysis.Tokens;

public interface IReadOnlyMemoryBuffer<T>
{
    public ReadOnlyMemory<T> Memory { get; }
    public ReadOnlySpan<T> Span { get; }

    public int Length { get; }
}
