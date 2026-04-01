using BenchmarkDotNet.Attributes;
using PySharp.Tokens;

namespace PySharp.Benchmarks;

[MemoryDiagnoser]
public class BenchTokenizer
{
    private static readonly string source_with_f_strings;
    private static readonly string source_without_f_strings;

    static BenchTokenizer()
    {
        source_with_f_strings = File.ReadAllText("/your/personal/path/to/file/with/fstrings");
        source_without_f_strings = File.ReadAllText("/your/personal/path/to/file/without/fstrings");
    }

    [Benchmark]
    public void TestTokenizeTriviaFalseFStrings()
    {
        var buffer = new StringBuffer(source_with_f_strings);
        var sync = SynchronizationPoint.ClearPoint(buffer);

        var tokenizer = new Tokenizer(sync, false);

        while (!tokenizer.ShouldStop)
            tokenizer.ReadNext();
    }

    [Benchmark]
    public void TestTokenizeTriviaTrueFStrings()
    {
        var buffer = new StringBuffer(source_with_f_strings);
        var sync = SynchronizationPoint.ClearPoint(buffer);

        var tokenizer = new Tokenizer(sync, true);

        while (!tokenizer.ShouldStop)
            tokenizer.ReadNext();
    }

    [Benchmark]
    public void TestTokenizeTriviaFalseRegular()
    {
        var buffer = new StringBuffer(source_without_f_strings);
        var sync = SynchronizationPoint.ClearPoint(buffer);

        var tokenizer = new Tokenizer(sync, false);

        while (!tokenizer.ShouldStop)
            tokenizer.ReadNext();
    }

    [Benchmark]
    public void TestTokenizeTriviaTrueRegular()
    {
        var buffer = new StringBuffer(source_without_f_strings);
        var sync = SynchronizationPoint.ClearPoint(buffer);

        var tokenizer = new Tokenizer(sync, true);

        while (!tokenizer.ShouldStop)
            tokenizer.ReadNext();
    }
}
